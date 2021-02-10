using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DHIS2Xfer.Factory;
using DHIS2Xfer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json.Linq;

namespace DHIS2Xfer.Controllers
{
    [Authorize]
    public class XferController : Controller
    {
        public string directory { get; }
        public IConfiguration Configuration { get; }

        public XferController(IConfiguration config)
        {
            Configuration = config;
            directory = Configuration["FileDirectory"];
        }

        public IActionResult Dashboard()
        {
            JArray servers = DataFactory.GetServers(directory);
            

            JArray OUMapping = DataFactory.GetOUMappingList(directory);
            JArray oumList = new JArray();

            foreach (JObject ou in OUMapping)
            {
                JObject meta = (JObject)ou.GetValue("meta");
                string sourceID = meta.GetValue("source").ToString();
                string destID = meta.GetValue("destination").ToString();
                string sourceName = "";
                string destName = "";

                foreach (JObject s in servers)
                {
                    if (sourceID == s.GetValue("ID").ToString())
                        sourceName = s.GetValue("name").ToString();

                    if (destID == s.GetValue("ID").ToString())
                        destName = s.GetValue("name").ToString();
                }

                JObject ouObj = new JObject();
                ouObj.Add("sourceID", sourceID);
                ouObj.Add("sourceName", sourceName);
                ouObj.Add("destID", destID);
                ouObj.Add("destName", destName);

                oumList.Add(ouObj);

            }

            JArray jobs = DataFactory.GetJobs(directory);
            JobStats js = new JobStats();

            foreach (JObject j in jobs)
            {
                JObject meta = (JObject)j.GetValue("meta");
                int jobCount = ((JArray)j.GetValue("jobs")).Count();
                string sourceDest = meta.GetValue("sourceName").ToString() + "/" + meta.GetValue("destinationName").ToString();
                string level = meta.GetValue("levelName").ToString();
                string periodType = "";
                switch (meta.GetValue("periodType").ToString())
                {
                    case "1":
                        periodType = "Daily";
                        break;
                    case "2":
                        periodType = "Monthly";
                        break;
                    case "3":
                        periodType = "Yearly";
                        break;
                    default:
                        break;
                }

                js.AddStat(sourceDest, jobCount,JobStats.StatType.SourceDestination);
                js.AddStat(level, jobCount, JobStats.StatType.Level);
                js.AddStat(periodType, jobCount, JobStats.StatType.Period);
            }

            JArray xfers = DataFactory.GetXfers(directory);

            ViewBag.servers = servers;
            ViewBag.oumList = oumList;
            ViewBag.serverCount = servers.Count();
            ViewBag.oumCount = oumList.Count();
            ViewBag.jobCount = jobs.Count();
            ViewBag.xferCount = xfers.Count();
            ViewBag.jobStats = js;

            return View();
        }

        public IActionResult Index()
        {
            JArray xferList = DataFactory.GetXfers(directory);

            ViewBag.xfers = xferList;

            return View();
        }

        public IActionResult Add()
        {
            JArray jobs = DataFactory.GetJobs(directory);

            ViewBag.jobs = jobs;

            return View();
        }

        public IActionResult Edit(string id)
        {
            JObject xfer = DataFactory.GetXfer(id, directory);
            JArray jobs = DataFactory.GetJobs(directory);

            ViewBag.xfer = xfer;
            ViewBag.jobs = jobs;

            return View();
        }

        public IActionResult Remove(string id)
        {
            FileFactory.RemoveFile(id, XferFactory.FileType.Xfer, directory);
            FileFactory.RemoveFile(id, XferFactory.FileType.XferMeta, directory);

            return RedirectToAction("Index");
        }


        public IActionResult SaveXfer(string id, string name, string periodStart, string periodEnd, string jobs)
        {
            JObject meta = new JObject();
            JObject Xfer = new JObject();
            JArray jobList = JArray.Parse(jobs);
            JArray selectedJobs = new JArray();
            JArray servers = new JArray();
            JArray ouMapping = new JArray();

            Guid xferID = Guid.NewGuid();
            if (id != null)
                xferID = Guid.Parse(id);

            meta["id"] = xferID.ToString();
            meta["name"] = name;
            meta["periodStart"] = periodStart;
            meta["periodEnd"] = periodEnd;
            meta["count"] = jobList.Count();
            meta["jobs"] = new JArray();

            Xfer["meta"] = meta;

            //Loop through and add job data
            foreach (var j in jobList)
            {
                string jobID = j.ToString();

                JArray metaJobs = meta["jobs"].Value<JArray>();
                metaJobs.Add(jobID);

                JObject job =  DataFactory.GetJob(jobID, directory);
                JObject jobMeta = (JObject)job.GetValue("meta");

                string source = jobMeta.GetValue("source").ToString();
                string destination = jobMeta.GetValue("destination").ToString();

                selectedJobs.Add(job);

                //Add server data for each job
                DataFactory.CheckServerExists(source, servers,directory);
                DataFactory.CheckServerExists(destination, servers, directory);

                DataFactory.CheckOUMappingExists(source, destination, ouMapping, directory);

            }

            Xfer["jobs"]= selectedJobs;
            Xfer["servers"] = servers;
            Xfer["ouMapping"] = ouMapping;

            FileFactory.WriteFile(Xfer, xferID.ToString(), XferFactory.FileType.Xfer, directory);
            //Meta file for when we want to list Xfer files but not load all the data
            FileFactory.WriteFile(meta, xferID.ToString(), XferFactory.FileType.XferMeta, directory);
            return Json(new { success = true });
        }

        public IActionResult RunXfer(string id,bool dryRun, string sessionID)
        {
            string xferDir = FileFactory.ValidateDirectory(directory, XferFactory.FileType.Xfer);
            XferCore.Core XferCore = new XferCore.Core();
            XferCore.SessionID = Guid.Parse(sessionID);

            XferCore.LogEvent += Core_LogEvent;

            try
            {
                XferCore.ProcessXfer(id, xferDir, dryRun);
            }
            catch (Exception ex)
            {

                return Json(new { success = false, msg = ex.Message });
            }

            string result = readLog(sessionID);

            //Remove the temp log file
            deleteLog(sessionID);

            return Json(new { success = true, log = result });
        }

        public IActionResult CheckStatus(string sessionID)
        {
            try
            {
                string result = readLog(sessionID);

                return Json(new { success = true, log = result });
            }
            catch (Exception ex)
            {

                return Json(new { success = false, msg = ex.Message });
            }

        }

        public FileResult downloadXfer(string id)
        {
            JObject xfer = DataFactory.GetXfer(id, directory);

            string dir = FileFactory.ValidateDirectory(directory, XferFactory.FileType.Xfer);
            string fileName = id + XferFactory.GetTypeExtension(XferFactory.FileType.Xfer);
            string path = dir + "\\";

            IFileProvider provider = new PhysicalFileProvider(path);
            IFileInfo fileInfo = provider.GetFileInfo(fileName);
            var readStream = fileInfo.CreateReadStream();
            var mimeType = "text/plain";
            return File(readStream, mimeType, xfer.GetValue("name").ToString() + ".xfer");
        }

        public FileResult generateManualImportFile(string id)
        {
            string xferDir = FileFactory.ValidateDirectory(directory, XferFactory.FileType.Xfer);
            XferCore.Core XferCore = new XferCore.Core();

            JObject data = XferCore.GenerateImportData(id, xferDir);

            JObject xfer = DataFactory.GetXfer(id, directory);

            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(data.ToString());
            writer.Flush();
            stream.Position = 0;

            var mimeType = "text/plain";
            return File(stream, mimeType, xfer.GetValue("name").ToString() + ".json");
        }

        private void Core_LogEvent(string log)
        {
            JObject jsonLog = JObject.Parse(log);

            string sessionID = jsonLog.GetValue("sessionID").ToString();
            string path = AppDomain.CurrentDomain.BaseDirectory + "Files\\" + sessionID + ".tmp";

            if (jsonLog["type"].ToString() == "log")
            {
                JObject meta = (JObject)jsonLog.GetValue("meta");
                JObject r = (JObject)jsonLog.GetValue("response");
                JObject importCount = (JObject)r.GetValue("importCount");
                JArray conflicts = null;

                if (r["conflicts"] != null)
                {
                    conflicts = (JArray)r.GetValue("conflicts");
                }

                string periodType = "";

                switch (meta["periodType"].ToString())
                {
                    case "1":
                        periodType = "Daily";
                        break;
                    case "2":
                        periodType = "Monthly";
                        break;
                    case "3":
                        periodType = "Yearly";
                        break;
                    default:
                        break;
                }

                string logCount = $"Processing: {meta["index"]} of {meta["total"]}";
                string logLine = $"DataElement: {meta["dimension"]}, Level: {meta["level"]}, Frequency: {periodType}, Period: {meta["period"]}, Status : {r["status"]}";
                string logLine2 = $"Imported: {importCount.GetValue("imported")} Update: {importCount.GetValue("updated")} Ignored: {importCount.GetValue("ignored")} Deleted: {importCount.GetValue("deleted")}";

                using (StreamWriter file = new StreamWriter(path, true))
                {
                    file.WriteLine($@"{logCount}");
                    file.WriteLine($@"{logLine}");
                    file.WriteLine($@"{logLine2}");

                    if (conflicts != null)
                    {
                        file.WriteLine($@"Conflicts:");

                        foreach (var c in conflicts)
                        {
                            file.WriteLine($@"{c["value"]}");
                        }
                    }

                    file.WriteLine($@"--------------------------------------------------------");
                }
            }

            if (jsonLog["type"].ToString() == "error")
            {
                using (StreamWriter file = new StreamWriter(path, true))
                {
                    file.WriteLine($@"An error occured, retrying...");
                    file.WriteLine($@"Message: {jsonLog["message"]}");
                    file.WriteLine($@"Detail: {jsonLog["detail"]}");
                }
            }
        }

        private string readLog(string sessionID)
        {
            string result = "";
            string path = AppDomain.CurrentDomain.BaseDirectory + "Files\\" + sessionID + ".tmp";

            if (!System.IO.File.Exists(path))
                return "";

            using (StreamReader file = new StreamReader(path))
            {
                result = file.ReadToEnd();
            }

            return result;
        }

        private void deleteLog(string sessionID)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "Files\\" + sessionID + ".tmp";
            System.IO.File.Delete(path);
        }
    }
}