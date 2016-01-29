using AutoMapper.Internal;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class Request {
        [JsonProperty("hostName")]
        public string HostName { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("httpMethod")]
        public string HttpMethod { get; set; }

        [JsonProperty("iPAddress")]
        public string IPAddress { get; set; }

        [JsonProperty("queryString")]
        public IDictionary QueryString { get; set; }

        public IList Cookies { get; set; }

        [JsonProperty("form")]
        public IDictionary Form { get; set; }

        public IDictionary Data { get; set; }

        [JsonProperty("headers")]
        public IDictionary Headers { get; set; }

        [JsonProperty("rawData")]
        public string RawData { get; set; }
    }
}
