using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DHIS2Xfer.Factory
{
    public class DataFactory
    {
        private static JArray GetFiles (XferFactory.FileType type, string directory)
        {
            //Get a list of all server files 
            List<string> list = FileFactory.GetFileList(type, directory);

            JArray objList = new JArray();

            foreach (var l in list)
            {
                JObject o = FileFactory.ReadFile(l);

                objList.Add(o);
            }

            return objList;
        }

        public static JArray GetServers(string directory)
        {
            //Get a list of all server files 
            List<string> serverList = FileFactory.GetFileList(XferFactory.FileType.Server, directory);

            JArray servers = new JArray();

            foreach (var s in serverList)
            {
                JObject server = FileFactory.ReadFile(s);

                servers.Add(server);
            }

            return servers;
        }

        public static JObject GetServer(string id,string directory)
        {
            return FileFactory.GetFile(id, XferFactory.FileType.Server, directory);
        }

        public static void RemoveServer(string id,string directory)
        {
            //Remove Server and all related meta data
            FileFactory.RemoveFile(id, XferFactory.FileType.Server, directory);
            FileFactory.RemoveFile(id, XferFactory.FileType.Meta, directory);
            FileFactory.RemoveFile(id, XferFactory.FileType.OrgUnitGroup, directory);
            FileFactory.RemoveFile(id, XferFactory.FileType.OrgUnitLevels, directory);
            FileFactory.RemoveFile(id, XferFactory.FileType.OrgUnits, directory);

            List<string> ouMapping = FileFactory.GetFileList(XferFactory.FileType.OUMapping, directory);

            foreach (var oum in ouMapping)
            {
                if (oum.Contains(id))
                {
                    string oumID = oum.Substring(oum.LastIndexOf('\\') + 1);
                    oumID = oumID.Substring(0,oumID.IndexOf('.'));

                    FileFactory.RemoveFile(oumID, XferFactory.FileType.OUMapping, directory);
                }
                    
            }

            //Remove jobs related to the server
            JArray jobs = GetJobs(directory);
            List<string> removedJobs = new List<string>();

            foreach (JObject j in jobs)
            {
                JObject meta = (JObject)j.GetValue("meta");

                string jobID = meta.GetValue("id").ToString();
                string source = meta.GetValue("source").ToString();
                string destination = meta.GetValue("destination").ToString();

                if (source == id || destination == id)
                {
                    FileFactory.RemoveFile(jobID, XferFactory.FileType.Job, directory);
                    removedJobs.Add(jobID);
                }
                    
            }

            //Remove jobs from the xfer files
            JArray xfers = DataFactory.GetXfers(directory);
            JArray keptJobs = new JArray();
            foreach (JObject x in xfers)
            {
                string xferID = x.GetValue("id").ToString();

                JArray xJobs = (JArray)x.GetValue("jobs");

                foreach (string j in xJobs)
                {
                    if (!removedJobs.Contains(j))
                        keptJobs.Add(j);
                }

                x["count"] = keptJobs.Count();
                x["jobs"] = keptJobs;

                //Rebuild the Xfer file using the meta data
                JObject newXfer = RebuildXfer(x,directory);

                FileFactory.WriteFile(newXfer, xferID.ToString(), XferFactory.FileType.Xfer, directory);
                //Meta file for when we want to list Xfer files but not load all the data
                FileFactory.WriteFile(x, xferID.ToString(), XferFactory.FileType.XferMeta, directory);
            }
        }

        public static JObject GetOrgUnit(string id, string server, string directory)
        {
            JObject orgUnits = GetOrgUnits(server, directory);
            
            foreach (JObject o in orgUnits.GetValue("organisationUnits"))
            {
                if (o.GetValue("id").ToString() == id)
                    return o;
            }

            return null;
        }
        public static JObject GetOrgUnits(string id, string directory)
        {
            return FileFactory.GetFile(id, XferFactory.FileType.OrgUnits, directory);
        }

        public static JObject GetOUMapping(string id, string directory)
        {
            return FileFactory.GetFile(id, XferFactory.FileType.OUMapping, directory);
        }

        public static JArray GetOUMappingList(string directory)
        {
            JArray list = GetFiles(XferFactory.FileType.OUMapping, directory);
            return list;
        }

        public static JObject GetOrgUnitLevel(string id, string server, string directory)
        {
            JObject orgUnitLevels = GetOrgUnitLevels(server, directory);

            foreach (JObject o in orgUnitLevels.GetValue("orgUnitLevels"))
            {
                if (o.GetValue("level").ToString() == id)
                    return o;
            }

            return null;
        }

        public static JObject GetOrgUnitLevels(string id, string directory)
        {
            return FileFactory.GetFile(id, XferFactory.FileType.OrgUnitLevels, directory);
        }

        public static JObject GetCategoryCombo(string id, string directory)
        {
            return FileFactory.GetFile(id, XferFactory.FileType.CategoryCombo, directory);
        }

        public static JObject GetCategoryOptionCombo(string id, string directory)
        {
            return FileFactory.GetFile(id, XferFactory.FileType.CategoryOptionCombo, directory);
        }

        public static JArray GetJobs(string directory)
        {
            return GetFiles(XferFactory.FileType.Job, directory);
        }

        public static JObject GetJob(string id, string directory)
        {
            return FileFactory.GetFile(id, XferFactory.FileType.Job, directory);
        }

        public static JArray GetXfers(string directory)
        {
            //Get a list of all server files 
            List<string> list = FileFactory.GetFileList(XferFactory.FileType.XferMeta, directory);

            JArray objList = new JArray();

            foreach (var l in list)
            {
                int index = l.IndexOf(".xfermeta");

                if(index != -1)
                {
                    JObject o = FileFactory.ReadFile(l);
                    objList.Add(o);
                }
                
            }

            return objList;
        }

        public static JObject GetXfer(string id, string directory)
        {
            return FileFactory.GetFile(id, XferFactory.FileType.XferMeta, directory);
        }

        public static JObject RebuildXfer(JObject meta,string directory)
        {
            JObject xfer = new JObject();
            JArray servers = new JArray();
            JArray selectedJobs = new JArray();
            JArray ouMapping = new JArray();
            JArray jobList = (JArray)meta.GetValue("jobs");

            foreach (var j in jobList)
            {
                string jobID = j.ToString();

                JObject job = DataFactory.GetJob(jobID, directory);
                JObject jobMeta = (JObject)job.GetValue("meta");

                string source = jobMeta.GetValue("source").ToString();
                string destination = jobMeta.GetValue("destination").ToString();

                selectedJobs.Add(job);

                //Add server data for each job
                CheckServerExists(source, servers, directory);
                CheckServerExists(destination, servers, directory);

                CheckOUMappingExists(source, destination, ouMapping, directory);

            }

            xfer["meta"] = meta;
            xfer["jobs"] = selectedJobs;
            xfer["servers"] = servers;
            xfer["ouMapping"] = ouMapping;

            return xfer;
        }

        public static JObject GetMeta(string id,string type, string directory)
        {
            return FileFactory.GetFile(id + XferFactory.GetTypeAbbrev(type), XferFactory.FileType.Meta, directory);
        }

        public static void CheckServerExists(string id, JArray servers,string directory)
        {
            bool exist = false;
            //Check if the server already exists
            foreach (JObject s in servers)
            {
                if (s.GetValue("ID").ToString() == id)
                {
                    exist = true;
                    break;
                }
            }

            if (!exist)
            {
                JObject server = DataFactory.GetServer(id, directory);
                servers.Add(server);
            }
        }

        public static void CheckOUMappingExists(string source, string destination, JArray ouMapping,string directory)
        {
            bool exist = false;
            //Check if the server already exists
            foreach (JObject ou in ouMapping)
            {
                JObject ouMeta = (JObject)ou.GetValue("meta");

                if (ouMeta.GetValue("source").ToString() == source && ouMeta.GetValue("destination").ToString() == destination)
                {
                    exist = true;
                    break;
                }
            }

            if (!exist)
            {
                JObject ouMap = DataFactory.GetOUMapping(source + "-" + destination, directory);
                ouMapping.Add(ouMap);
            }
        }
    }
}
