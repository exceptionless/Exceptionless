using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class Details {

        [JsonProperty("machineName")]
        public string MachineName { get; set; }

        [JsonProperty("groupingKey")]
        public string GroupingKey { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("client")]
        public Client Client { get; set; }

        [JsonProperty("error")]
        public Error Error { get; set; }

        [JsonProperty("environment")]
        public Environment Environment { get; set; }

        [JsonProperty("tags")]
        public IList<string> Tags { get; set; }

        [JsonProperty("userCustomData")]
        public IDictionary UserCustomData { get; set; }

        [JsonProperty("request")]
        public Request Request { get; set; }

        [JsonProperty("response")]
        public Response Response { get; set; }

        [JsonProperty("user")]
        public User User { get; set; }
    }
}
