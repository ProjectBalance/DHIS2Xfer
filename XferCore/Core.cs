using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XferCore
{
    public class Core
    {
        public JObject meta;
        public JArray jobs;
        public JArray servers;
        public JArray ouMapping;
        public List<JArray> manualImports;

        public JArray Log;

        public delegate void LogEventHandler(string log);
        public event LogEventHandler LogEvent;

        public Guid SessionID;

        public List<OUMap> OUMapList = new List<OUMap>();

        /// <summary>
        /// Process all Xfer files within the directory
        /// </summary>
        /// <param name="directory">Directory which holds the .xfer files</param>
        public void ProcessXfers(string directory, bool dryRun)
        {
            List<string> files = FileFactory.GetFileList(directory);
            foreach (string f in files)
            {
                //Only load .xfer files
                if (Path.GetExtension(f) == ".xfer")
                {
                    string id = Path.GetFileNameWithoutExtension(f);
                    ProcessXfer(id, directory, dryRun);
                }

            }
        }

        /// <summary>
        /// Process a single Xfer file
        /// </summary>
        /// <param name="id">Xfer ID</param>
        /// <param name="directory">Directory which holds the .xfer files</param>
        public void ProcessXfer(string id, string directory, bool dryRun)
        {
            if (InitializeXfer(id, directory))
                ProcessJobs(dryRun, true).Wait();
        }



        public JObject GenerateImportData(string id, string directory)
        {
            manualImports = new List<JArray>();
            InitializeXfer(id, directory);
            ProcessJobs(true, false).Wait();

            JObject result = new JObject();
            JArray data = new JArray();

            //Loop through all data and combine into one 

            foreach (var i in manualImports)
            {
                foreach (JObject d in i)
                {
                    data.Add(d);
                }
            }

            result["dataValues"] = data;

            return result;
        }

        private bool InitializeXfer(string id, string directory)
        {
            string path = directory + "\\" + id + ".xfer";
            if (File.Exists(path))
            {
                //Get the Xfer file
                JObject xfer = FileFactory.GetFile(id, directory);

                //Load all needed data from the xfer file to process
                meta = (JObject)xfer.GetValue("meta");
                jobs = (JArray)xfer.GetValue("jobs");
                servers = (JArray)xfer.GetValue("servers");
                ouMapping = (JArray)xfer.GetValue("ouMapping");

                LoadOUMap();

                Log = new JArray();

                return true;
            }
            else
            {
                if (LogEvent != null)
                    LogEvent(id + " does not exist within the provided directory");

                return false;
            }
        }

        private async Task ProcessJobs(bool dryRun, bool runExport)
        {
            foreach (JObject job in jobs)
            {
                //JArray xferExports = new JArray();

                JObject jobMeta = (JObject)job.GetValue("meta");
                JArray jobList = (JArray)job.GetValue("jobs");

                JObject sourceServer = FindServer(jobMeta.GetValue("source").ToString());
                JObject destinationServer = FindServer(jobMeta.GetValue("destination").ToString());

                string baseUrl = sourceServer.GetValue("url").ToString();

                if (baseUrl.Substring(baseUrl.Length - 1) != "/")
                    baseUrl += "/";

                //Load the current OU Mapping for this job
                OUMap OUcurrent = OUMapList.Where(w => w.SourceID == sourceServer.GetValue("ID").ToString() && w.DestinationID == destinationServer.GetValue("ID").ToString()).FirstOrDefault();
                int index = 0;

                foreach (JObject j in jobList)
                {
                    index += 1;

                    JObject xferObject = new JObject();
                    JArray xferList = new JArray();
                    JObject xferMeta = new JObject();

                    xferMeta["index"] = index;
                    xferMeta["total"] = jobList.Count;
                    xferMeta["dimension"] = j["srcName"];
                    xferMeta["period"] = $"{meta["periodStart"]} - {meta["periodEnd"]}";
                    xferMeta["periodType"] = jobMeta["periodType"];
                    xferMeta["level"] = jobMeta["level"];

                    List<string> periods = GetPeriods(int.Parse(jobMeta.GetValue("periodType").ToString()), meta.GetValue("periodStart").ToString(), meta.GetValue("periodEnd").ToString());

                    string period = "";
                    //Concatenate all the periods together to pull all data at once
                    foreach (string p in periods)
                    {
                        period += p + ";";
                    }

                    string url = baseUrl + "api/analytics.json?skipMeta=true&paging=false&dimension=ou:LEVEL-" + jobMeta.GetValue("level").ToString() + "&dimension=pe:" + period + "&dimension=dx:" + j.GetValue("sourceID");
                    bool retry = true;
                    int retryCount = 0;

                    JObject data = new JObject();
                    string result = "";

                    //If the data that comes back does not parse, log it and retry. This is to prevent a possible network issue
                    while (retry)
                    {
                        result = await HTTPFactory.GET(url, sourceServer.GetValue("user").ToString(), sourceServer.GetValue("password").ToString());

                        try
                        {
                            data = JObject.Parse(result);
                            retry = false;
                        }
                        catch (Exception ex)
                        {
                            //Catch the error, log it and retry
                            retryCount += 1;

                            JObject log = new JObject();
                            log["type"] = "error";
                            log["message"] = ex.Message;
                            log["detail"] = result;

                            if (SessionID != Guid.Empty)
                                log["sessionID"] = SessionID;

                            if (LogEvent != null)
                                LogEvent(log.ToString());
                        }
                        
                        //After 5 failed attempts, throw and error. 
                        if(retryCount == 5)
                        {
                            throw new Exception("Network error occured");
                        }
                    }

                    try
                    {


                        //Don't process if there is no result
                        if (result != "")
                        {
                            JArray rows = (JArray)data.GetValue("rows");

                            foreach (JArray r in rows)
                            {
                                string ouID = r[1].ToString();
                                var value = r[3];
                                string periodValue = r[2].ToString();

                                //Check if there is a matching OrgUnit 
                                //string destOU = MatchOU(sourceServer.GetValue("ID").ToString(), destinationServer.GetValue("ID").ToString(), ouID);
                                string destOU = OUcurrent.Mapped.Where(w => w.SourceID == ouID).Select(s => s.DestinationID).FirstOrDefault();

                                //Match was found
                                if (destOU != "")
                                {
                                    JObject xfer = new JObject();
                                    xfer["dataElement"] = j.GetValue("destID").ToString();
                                    xfer["orgUnit"] = destOU;
                                    xfer["period"] = periodValue;

                                    //Only add COC if there is one
                                    if (j.GetValue("destCOC").ToString() != "-1" && j.GetValue("destCOC").ToString() != "")
                                        xfer["categoryOptionCombo"] = j.GetValue("destCOC").ToString();

                                    //xfer["attributeOptionCombo"] = "";
                                    xfer["value"] = value;
                                    xfer["storedBy"] = destinationServer.GetValue("user").ToString(); //Since its optional to use the username, we can skip an api hit to credentials
                                    xfer["created"] = DateTime.Now.ToString("yyyy-MM-dd");
                                    xfer["updated"] = DateTime.Now.ToString("yyyy-MM-dd");
                                    xfer["comment"] = "Imported";

                                    xferList.Add(xfer);
                                }
                            }

                        }

                        //If true will continue the process to export the data to the destination server
                        if (runExport)
                        {
                            string baseUrlDest = destinationServer.GetValue("url").ToString();
                            //foreach (JObject x in xferExports)
                            //{
                            JObject xfer = new JObject();
                            xfer["dataValues"] = xferList;
                            string exportData = xfer.ToString();

                            //string data = "{\"dataValues\":[" + xfer.ToString() + "]}";
                            string postUrl = baseUrlDest + "api/dataValueSets?importStrategy=CREATE_AND_UPDATE&format=json&preheatCache=false&skipExistingCheck=false&skipAudit=false&dryRun=" + dryRun;
                            string response = await HTTPFactory.POST(postUrl, sourceServer.GetValue("user").ToString(), sourceServer.GetValue("password").ToString(), exportData);
                            

                            try
                            {
                                JObject r = JObject.Parse(response);
                                JObject log = new JObject();

                                if (SessionID != Guid.Empty)
                                    log["sessionID"] = SessionID;

                                log["type"] = "log";
                                log["meta"] = xferMeta;
                                log["response"] = r;

                                if (LogEvent != null)
                                    LogEvent(log.ToString());

                                Log.Add(log);
                            }
                            catch(Exception ex)
                            {
                                JObject logError = new JObject();
                                logError["type"] = "error";
                                logError["message"] = ex.Message;
                                logError["detail"] = response;

                                if (SessionID != Guid.Empty)
                                    logError["sessionID"] = SessionID;

                                if (LogEvent != null)
                                    LogEvent(logError.ToString());

                                //Give the log event time to write to the file
                                Thread.Sleep(5000);
                                throw new Exception("An error occured processing a reponse.");
                            }

                        }
                        else
                        {
                            manualImports.Add(xferList);
                        }

                    }
                    catch (Exception ex)
                    {
                        JObject log = new JObject();
                        log["type"] = "error";
                        log["message"] = ex.Message;
                        log["detail"] = result;

                        if (SessionID != Guid.Empty)
                            log["sessionID"] = SessionID;

                        if (LogEvent != null)
                            LogEvent(log.ToString());

                        //Give the log event time to write to the file
                        Thread.Sleep(5000);
                        throw new Exception("An Error Occuried.");
                    }
                }

            }
        }



        public JArray GetLog()
        {
            return Log;
        }

        private JObject FindServer(string id)
        {
            foreach (JObject server in servers)
            {
                if (server.GetValue("ID").ToString() == id)
                    return server;
            }

            return null;
        }

        private List<string> GetPeriods(int type, string start, string end)
        {
            List<string> periods = new List<string>();
            DateTime dtStart = DateTime.Parse(start);
            DateTime dtEnd = DateTime.Parse(end);
            DateTime dtCurrent = dtStart;

            while (dtCurrent <= dtEnd)
            {
                string period = "";
                string year = dtCurrent.Year.ToString();
                string month = dtCurrent.Month.ToString();
                string day = dtCurrent.Day.ToString();

                if (dtCurrent.Month < 10)
                    month = "0" + dtCurrent.Month.ToString();

                if (dtCurrent.Day < 10)
                    day = "0" + dtCurrent.Day.ToString();

                switch (type)
                {

                    case 1: //Daily
                        period = year + month + day;
                        dtCurrent = dtCurrent.AddDays(1);
                        break;
                    case 2: //Monthly
                        period = year + month;
                        dtCurrent = dtCurrent.AddMonths(1);
                        break;
                    case 3: //Yearly
                        period = year;
                        dtCurrent = dtCurrent.AddYears(1);
                        break;
                    default:
                        break;
                }

                periods.Add(period);
            }


            return periods;
        }

        private JObject FindOUMap(string source, string destination)
        {
            foreach (JObject ouMap in ouMapping)
            {
                JObject ouMeta = (JObject)ouMap.GetValue("meta");
                if (ouMeta.GetValue("source").ToString() == source && ouMeta.GetValue("destination").ToString() == destination)
                    return ouMap;
            }

            return null;
        }
        private string MatchOU(string source, string destination, string ou)
        {
            JObject ouMap = FindOUMap(source, destination);

            JArray mapped = (JArray)ouMap.GetValue("mapped");

            foreach (JObject ouM in mapped)
            {
                if (ouM.GetValue("sourceID").ToString() == ou)
                    return ouM.GetValue("destID").ToString();
            }

            return "";
        }

        private void LoadOUMap()
        {
            foreach (JObject ouMap in ouMapping)
            {
                JObject ouMeta = (JObject)ouMap.GetValue("meta");
                OUMap oum = new OUMap();
                oum.SourceID = ouMeta["source"].ToString();
                oum.DestinationID = ouMeta["destination"].ToString();

                JArray mapped = (JArray)ouMap.GetValue("mapped");

                foreach (JObject ouM in mapped)
                {
                    OUMapData oumd = new OUMapData();
                    oumd.SourceID = ouM["sourceID"].ToString();
                    oumd.DestinationID = ouM["destID"].ToString();

                    oum.Mapped.Add(oumd);
                }

                OUMapList.Add(oum);
            }
        }
    }
}

public class OUMap
{
    public string SourceID { get; set; }
    public string DestinationID { get; set; }

    public List<OUMapData> Mapped { get; set; }

    public OUMap()
    {
        Mapped = new List<OUMapData>();
    }
}
public class OUMapData
{
    public string SourceID { get; set; }
    public string DestinationID { get; set; }

    
}