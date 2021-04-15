using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DHIS2Xfer.Models.Job
{
    public class UnMapped
    {
        public string ID { get; set; }
        public string Type { get; set; }
        public string SelectID { get; set; }
        public string SelectOptionID { get; set; }
        public JArray Data { get; set; }
    }
}
