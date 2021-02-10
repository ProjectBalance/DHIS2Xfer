using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Mvc;
using DHIS2Xfer.Factory;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.HSSF.Util;
using System.IO;
using DHIS2Xfer.Models;

namespace DHIS2Xfer.Controllers
{
    [Authorize]
    public class ManageController : Controller
    {
        public string directory { get; }
        public IConfiguration Configuration { get; }

        public ManageController(IConfiguration config)
        {
            Configuration = config;
            directory = Configuration["FileDirectory"];
        }

        public IActionResult Index()
        {
            return View();
        }

        #region Server

        public IActionResult Servers()
        {
            JArray data = DataFactory.GetServers(directory);

            ViewBag.data = data;

            return View();
        }

        public IActionResult AddServer()
        {

            return View();
        }

        public IActionResult EditServer(string id)
        {

            JObject data = DataFactory.GetServer(id, directory);

            ViewBag.id = id;
            ViewBag.data = data;

            return View();
        }

        [HttpPost]
        public IActionResult AddEditServer(string serverID, string name, string url, string type, string user, string password)
        {
            Guid id = Guid.NewGuid();

            //Check if we are editing the server info
            if (serverID != "" && serverID != null)
                id = Guid.Parse(serverID);

            var server = new JObject();
            server["ID"] = id.ToString();
            server["name"] = name;
            server["url"] = url;
            //server["type"] = type;
            server["user"] = user;
            server["password"] = password;
            server["lastSync"] = "";

            FileFactory.WriteFile(server, id.ToString(), XferFactory.FileType.Server,directory);


            return RedirectToAction("Servers");
        }

        public IActionResult RemoveServer(string id)
        {
            DataFactory.RemoveServer(id, directory);
            return RedirectToAction("Servers");
        }

        public IActionResult TestConnection(string url,string username, string password)
        {
            if (url.Length > 1)
                if (url.Substring(url.Length - 1) != "/")
                    url = url + "/";

            string api = url + "api/dataElements?format=json&paging=true&pageSize=1";
            string result = HTTPFactory.HTTPGet(api, username, password);

            if(result != "")
                return Json(new { success = true, msg = "Successful connection" });
            else
                return Json(new { success = false, msg = "Connection failed" });
        }

        public IActionResult SyncServer(string id)
        {

            JObject server = DataFactory.GetServer(id, directory);

            string url = server.GetValue("url").ToString();
            if (url.Length > 1)
                if (url.Substring(url.Length - 1) != "/")
                    url = url + "/";

            string username = server.GetValue("user").ToString();
            string password = server.GetValue("password").ToString();

            

            //Get data

            string api = url + Configuration["api:dataElements"];
            string result = HTTPFactory.HTTPGet(api, username, password);
            if(result == "")
                return Json(new { success = false, msg = "Unable to complete Data Elements sync, please try again" });

            WriteToFile(result, id + "-DE", XferFactory.FileType.Meta);

            api = url + Configuration["api:indicators"];
            result = HTTPFactory.HTTPGet(api, username, password);
            if (result == "")
                return Json(new { success = false, msg = "Unable to complete Indicator sync, please try again" });
            WriteToFile(result, id + "-IND", XferFactory.FileType.Meta);

            api = url + Configuration["api:programIndicators"];
            result = HTTPFactory.HTTPGet(api, username, password);
            if (result == "")
                return Json(new { success = false, msg = "Unable to complete Program Indicator sync, please try again" });
            WriteToFile(result, id + "-PRG", XferFactory.FileType.Meta);

            //Get Organisation Units
            api = url + Configuration["api:organisationUnits"];
            result = HTTPFactory.HTTPGet(api, username, password);
            if (result == "")
                return Json(new { success = false, msg = "Unable to complete Organisation Unit sync, please try again" });
            WriteToFile(result, id, XferFactory.FileType.OrgUnits);

            //Get Organisation Unit Groups
            api = url + Configuration["api:organisationUnitGroups"];
            result = HTTPFactory.HTTPGet(api, username, password);
            if (result == "")
                return Json(new { success = false, msg = "Unable to complete Organisation Unit Group sync, please try again" });
            WriteToFile(result, id, XferFactory.FileType.OrgUnitGroup);

            //Get Organisation Unit Levels
            api = url + Configuration["api:organisationUnitLevels"];

            result = HTTPFactory.HTTPGet(api, username, password);
            JObject levels = new JObject();
            if(result != "")
            {
                levels["orgUnitLevels"] = JArray.Parse(result);
                FileFactory.WriteFile(levels, id, XferFactory.FileType.OrgUnitLevels, directory);
            }

            //Get Category Combos
            api = url + Configuration["api:categoryCombo"];

            result = HTTPFactory.HTTPGet(api, username, password);
            if (result == "")
                return Json(new { success = false, msg = "Unable to complete Category Combo sync, please try again" });
            WriteToFile(result, id, XferFactory.FileType.CategoryCombo);

            //Get Category Option Combos
            api = url + Configuration["api:categoryOptionCombo"];

            result = HTTPFactory.HTTPGet(api, username, password);
            if (result == "")
                return Json(new { success = false, msg = "Unable to complete Category Combo Option sync, please try again" });
            WriteToFile(result, id, XferFactory.FileType.CategoryOptionCombo);

            //Add the sync time
            server["lastSync"] = DateTime.Now;
            FileFactory.WriteFile(server, id, XferFactory.FileType.Server, directory);

            return Json(new { success = true, result = DateTime.Now});
        }

        #endregion 

        public void WriteToFile(string result,string id, XferFactory.FileType type)
        {
            if(result != "")
            {
                JObject data = JObject.Parse(result);
                FileFactory.WriteFile(data, id, type, directory);
            }
            
        }

        public IActionResult CategoryOptionCombo()
        {
            return View();
        }

        public IActionResult OrgUnits()
        {
            JArray servers = DataFactory.GetServers(directory);

            ViewBag.servers = servers;

            return View();
        }

        public IActionResult LoadOrgUnitMapping(string source, string destination)
        {
            JObject ouMapped = GetOUMapping(source, destination);

            ViewBag.ouMapped = ouMapped;

            return PartialView("~/Views/Manage/OrgUnit/OUMapper.cshtml");
        }

        public IActionResult SaveOUMapping(string source, string destination,string data)
        {
            try
            {
                JObject map = JObject.Parse(data);

                FileFactory.WriteFile(map, source + "-" + destination, XferFactory.FileType.OUMapping, directory);

                JObject flippedMap = FlipOuMap(map);
                //Save a version that has been flipped round
                FileFactory.WriteFile(flippedMap, destination + "-" + source, XferFactory.FileType.OUMapping, directory);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false });
            }
           
        }

        public FileResult exportMapping(string s, string d)
        {
            //If a source or destination is missing return an empty text file
            if(s == "-1" || d == "-1")
            {

                var ms = new MemoryStream();
                TextWriter tw = new StreamWriter(ms);
                tw.Write("Invalid Source or Destination selection");
                tw.Flush();
                ms.Position = 0;

                var mt = "text/plain";

                return File(ms, mt, "invalid.txt");
                
            }
                
            IWorkbook workbook = new XSSFWorkbook();
            var sheetMapped = workbook.CreateSheet("Mapped");
            var sheetUnMapped = workbook.CreateSheet("Unmapped");

            JObject ouMap = GetOUMapping(s, d);
            JArray mapped = (JArray)ouMap.GetValue("mapped");
            JArray unmapped = (JArray)ouMap.GetValue("unmapped");

            JObject serverSource = DataFactory.GetServer(s, directory);
            JObject serverDestination = DataFactory.GetServer(d, directory);

            string soureName = serverSource.GetValue("name").ToString();
            string destinationName = serverDestination.GetValue("name").ToString();

            int rowIndex = 0;

            sheetMapped.CreateRow(rowIndex);
            sheetMapped.GetRow(rowIndex).CreateCell(0).SetCellValue("Source ID");
            sheetMapped.GetRow(rowIndex).CreateCell(1).SetCellValue("Source Name");
            sheetMapped.GetRow(rowIndex).CreateCell(2).SetCellValue("Destination ID");
            sheetMapped.GetRow(rowIndex).CreateCell(3).SetCellValue("Destination Name");

            rowIndex++;

            foreach (JObject m in mapped)
            {
                sheetMapped.CreateRow(rowIndex);
                sheetMapped.GetRow(rowIndex).CreateCell(0).SetCellValue(m.GetValue("sourceID").ToString());
                sheetMapped.GetRow(rowIndex).CreateCell(1).SetCellValue(m.GetValue("srcName").ToString());
                sheetMapped.GetRow(rowIndex).CreateCell(2).SetCellValue(m.GetValue("destID").ToString());
                sheetMapped.GetRow(rowIndex).CreateCell(3).SetCellValue(m.GetValue("destName").ToString());

                rowIndex++;
            }

            //Reset the row count for the next sheet
            rowIndex = 0;

            sheetUnMapped.CreateRow(rowIndex);
            sheetUnMapped.GetRow(rowIndex).CreateCell(0).SetCellValue("Source ID");
            sheetUnMapped.GetRow(rowIndex).CreateCell(1).SetCellValue("Source Name");
            sheetUnMapped.GetRow(rowIndex).CreateCell(2).SetCellValue("Destination ID");
            sheetUnMapped.GetRow(rowIndex).CreateCell(3).SetCellValue("Destination Name");

            rowIndex++;

            foreach (JObject um in unmapped)
            {
                var type = um.GetValue("type").ToString();
                if(type == "Source")
                {
                    sheetUnMapped.CreateRow(rowIndex);
                    sheetUnMapped.GetRow(rowIndex).CreateCell(0).SetCellValue(um.GetValue("id").ToString());
                    sheetUnMapped.GetRow(rowIndex).CreateCell(1).SetCellValue(um.GetValue("displayName").ToString());

                    rowIndex++;
                }
                
            }

            rowIndex = 1;

            foreach (JObject um in unmapped)
            {
                var type = um.GetValue("type").ToString();
                if (type == "Destination")
                {
                    if(sheetUnMapped.GetRow(rowIndex) == null)
                        sheetUnMapped.CreateRow(rowIndex);

                    sheetUnMapped.GetRow(rowIndex).CreateCell(2).SetCellValue(um.GetValue("id").ToString());
                    sheetUnMapped.GetRow(rowIndex).CreateCell(3).SetCellValue(um.GetValue("displayName").ToString());

                    rowIndex++;
                }

            }


            NPOIMemoryStream stream = new NPOIMemoryStream();
                stream.AllowClose = false;

                workbook.Write(stream);

                stream.Flush();                
                stream.Seek(0, SeekOrigin.Begin);
                stream.Position = 0;
                stream.AllowClose = true;

                var mimeType = "application/vnd.ms-excel";

                return File(stream, mimeType, $"OU Mapping {soureName}-{destinationName}-{DateTime.Now}.xlsx"); ;

        }

        private JObject GetOUMapping(string source, string destination)
        {
            string ouMapID = source + "-" + destination;

            JObject ouMapping = DataFactory.GetOUMapping(ouMapID, directory);

            JObject ouMapped = new JObject();

            if (ouMapping != null)
            {
                //TODO: Validate the ouMapping file with the ouSource and ouDest
                ouMapped = XferFactory.verifyOuMapping(ouMapping, source, destination, directory);
            }
            else
            {
                //Auto Map source and destination
                ouMapped = XferFactory.OUAutoMap(source, destination, directory);
            }

            return ouMapped;
        }

        private JObject FlipOuMap(JObject ouMap)
        {
            JObject flippedMap = new JObject();

            JObject meta = (JObject)ouMap.GetValue("meta");
            JArray mapped = (JArray)ouMap.GetValue("mapped");
            JArray unmapped = (JArray)ouMap.GetValue("unmapped");

            JObject fmeta = new JObject();
            JArray fmapped = new JArray();
            JArray funmapped = new JArray();

            fmeta["source"] = meta.GetValue("destination");
            fmeta["destination"] = meta.GetValue("source");
            fmeta["sourceSync"] = meta.GetValue("destSync");
            fmeta["destSync"] = meta.GetValue("sourceSync");

            foreach (JObject m in mapped)
            {
                JObject fm = new JObject();
                fm["sourceID"] = m.GetValue("destID");
                fm["destID"] = m.GetValue("sourceID");
                fm["srcName"] = m.GetValue("destName");
                fm["destName"] = m.GetValue("srcName");

                fmapped.Add(fm);
            }

            foreach (JObject um in unmapped)
            {
                JObject fum = um;
                if (um.GetValue("type").ToString() == "Source")
                    fum["type"] = "Destination";
                else
                    fum["type"] = "Source";

                funmapped.Add(fum);
            }

            flippedMap["meta"] = fmeta;
            flippedMap["mapped"] = fmapped;
            flippedMap["unmapped"] = funmapped;

            return flippedMap; 
        }
    }
}