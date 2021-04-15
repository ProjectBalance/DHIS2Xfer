using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using DHIS2Xfer.Factory;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;

namespace DHIS2Xfer.Controllers
{
    [Authorize]
    public class JobController : Controller
    {
        public string directory { get; }
        public IConfiguration Configuration { get; }

        public JobController(IConfiguration config)
        {
            Configuration = config;
            directory = Configuration["FileDirectory"];
        }

        public IActionResult Index()
        {
            JArray jobs = DataFactory.GetJobs(directory);

            ViewBag.data = jobs;

            return View();
        }

        public IActionResult Add()
        {
            JArray servers = DataFactory.GetServers(directory);

            ViewBag.servers = servers;

            return View();
        }

        public IActionResult Edit(string id)
        {
            JArray servers = DataFactory.GetServers(directory);

            ViewBag.servers = servers;

            JObject job = DataFactory.GetJob(id, directory);

            ViewBag.job = job;

            return View();
        }

        public IActionResult Remove(string id)
        {
            FileFactory.RemoveFile(id, XferFactory.FileType.Job, directory);

            //Remove job from xfer files
            JArray xfers = DataFactory.GetXfers(directory);
            JArray keptJobs = new JArray();
            foreach (JObject x in xfers)
            {
                string xferID = x.GetValue("id").ToString();

                JArray xJobs = (JArray)x.GetValue("jobs");

                foreach (string j in xJobs)
                {
                    if (j != id)
                        keptJobs.Add(j);
                }

                x["count"] = keptJobs.Count();
                x["jobs"] = keptJobs;

                //Rebuild the Xfer file using the meta data
                JObject newXfer = DataFactory.RebuildXfer(x, directory);

                FileFactory.WriteFile(newXfer, xferID.ToString(), XferFactory.FileType.Xfer, directory);
                //Meta file for when we want to list Xfer files but not load all the data
                FileFactory.WriteFile(x, xferID.ToString(), XferFactory.FileType.XferMeta, directory);
            }

            return RedirectToAction("Index");
        }

        public IActionResult LoadJobMeta(string id, string name, string source, string destination, string sourceType, string destinationType)
        {

            JObject ouLevels = DataFactory.GetOrgUnitLevels(source, directory);
            JObject jobs = XferFactory.jobAutoMap(source, destination, sourceType, destinationType, directory);
            JObject job = DataFactory.GetJob(id, directory);

            if(job != null)
            {
                JArray mappedJobs = (JArray)job.GetValue("jobs");
                jobs["mapped"] = mappedJobs;

                JObject meta = (JObject)job.GetValue("meta");
                ViewBag.meta = meta;
            }
            


            ViewBag.ouLevels = ouLevels;
            ViewBag.jobs = jobs;

            return PartialView("~/Views/Job/Partial/JobMeta.cshtml");
        }


        public IActionResult LoadCOC(string id, string sID, string sType)
        {
            //Get server data
            JObject server = DataFactory.GetServer(sID, directory);

            string url = server.GetValue("url").ToString();
            string username = server.GetValue("user").ToString();
            string password = server.GetValue("password").ToString();
            string type = sType;

            if (url.Length > 1)
                if (url.Substring(url.Length - 1) != "/")
                    url = url + "/";


            //Only data elements have category options
            if (type == "dataElements")
            {
                JObject dataElement = DataFactory.GetMeta(sID,sType, directory);
                JObject categoryCombos = DataFactory.GetCategoryCombo(sID, directory);
                JArray categoryOptionCombos = new JArray();

                string ccID = "";

                foreach (JObject de in dataElement["dataElements"])
                {
                    string deID = de.GetValue("id").ToString();
                    if(deID == id)
                    {
                        JObject cc = (JObject)de.GetValue("categoryCombo");
                        ccID = cc.GetValue("id").ToString();
                        break;
                    }
                    
                }

                foreach (JObject catCombo in categoryCombos["categoryCombos"])
                {
                    if(catCombo.GetValue("id").ToString() == ccID)
                    {
                        categoryOptionCombos = (JArray)catCombo.GetValue("categoryOptionCombos");
                    }
                }


                ////Get the Category Option Combo data file
                JObject cocFile = DataFactory.GetCategoryOptionCombo(sID, directory);
                JArray cocList = (JArray)cocFile.GetValue("categoryOptionCombos");

                JArray cocResult = new JArray();

                //Find all the COCs for the Category Combo
                foreach (JObject c in categoryOptionCombos)
                {
                    string cocID = c.GetValue("id").ToString();

                    foreach (JObject coc in cocList)
                    {
                        if (coc.GetValue("id").ToString() == cocID)
                            cocResult.Add(coc);
                    }


                }

                return Json(cocResult.ToString());
            }
            else
            {
                string na = "[{\"id\":na,\"displayName\":\"NA\"}]";
                return Json(na);
            }
        }

        public IActionResult SaveJob(string id, string name, string orgUnitLevel,string periodType, string source, string destination,string sourceType, string destinationType, string data)
        {
            try
            {
                JObject job = new JObject();
                JObject meta = new JObject();
                JObject map = JObject.Parse(data);
                JObject ouLvl = DataFactory.GetOrgUnitLevel(orgUnitLevel, source, directory);

                JObject src = DataFactory.GetServer(source, directory);
                JObject dest = DataFactory.GetServer(destination, directory);

                Guid ID = Guid.NewGuid();
                if (id != "" && id != null)
                    ID = Guid.Parse(id);

                meta["id"] = ID.ToString();
                meta["name"] = name;
                meta["levelName"] = ouLvl.GetValue("displayName").ToString(); 
                meta["level"] = ouLvl.GetValue("level").ToString();
                meta["periodType"] = periodType;
                meta["source"] = source;
                meta["destination"] = destination;
                meta["sourceName"] = src.GetValue("name").ToString();
                meta["destinationName"] = dest.GetValue("name").ToString(); ;
                meta["sourceType"] = sourceType;
                meta["destinationType"] = destinationType;

                job["meta"] = meta;
                job["jobs"] = map.GetValue("mapped");

                FileFactory.WriteFile(job, ID.ToString(), XferFactory.FileType.Job, directory);

                if (id != "" && id != null) //Only check if we are editing a job
                {
                    //Loop through existing xfer files and update them to match the job edits
                    JArray xfers = DataFactory.GetXfers(directory);

                    foreach (JObject x in xfers)
                    {
                        string xferID = x.GetValue("id").ToString();

                        JArray xJobs = (JArray)x.GetValue("jobs");

                        foreach (string j in xJobs)
                        {
                            if (j == id) //If the job exists in the file, then we rebuild it
                            {
                                JObject xfer = DataFactory.RebuildXfer(x, directory);
                                FileFactory.WriteFile(xfer, xferID, XferFactory.FileType.Xfer, directory);
                                break;
                            }
                        }


                    }
                }
                

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false });
            }

        }
    }
}