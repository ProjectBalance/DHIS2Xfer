using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DHIS2Xfer.Factory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace DHIS2Xfer.Controllers
{
    public class DataController : Controller
    {
        public string directory { get; }
        public IConfiguration Configuration { get; }

        public DataController(IConfiguration config)
        {
            Configuration = config;
            directory = Configuration["FileDirectory"];
        }

        public IActionResult Index()
        {
            JArray servers = DataFactory.GetServers(directory);

            ViewBag.servers = servers;

            return View();
        }

        public IActionResult LoadMeta(string id, string type)
        {
            JObject meta = DataFactory.GetMeta(id, type, directory);

            ViewBag.meta = meta;
            ViewBag.type = type;

            return PartialView("~/Views/Data/Partial/DataViewer.cshtml");
        }

        public IActionResult LoadData(string id, string type, string serverId)
        {
            JObject server = DataFactory.GetServer(serverId, directory);

            string url = server.GetValue("url").ToString();
            if (url.Length > 1)
                if (url.Substring(url.Length - 1) != "/")
                    url = url + "/";

            string username = server.GetValue("user").ToString();
            string password = server.GetValue("password").ToString();


            string api = url + "api/analytics.json?skipMeta=false&paging=false&dimension=pe:LAST_5_YEARS&dimension=dx:" + id;
            string result = HTTPFactory.HTTPGet(api, username, password);

            return Json(result.ToString());
        }
    }
}
