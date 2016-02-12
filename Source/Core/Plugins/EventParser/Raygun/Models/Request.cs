using System;
using System.Collections.Generic;
using System.Linq;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class Request {
        public string HostName { get; set; }

        public string Url { get; set; }

        public string HttpMethod { get; set; }

        public string IPAddress { get; set; }

        public IDictionary<string, string> QueryString { get; set; }
        
        public IDictionary<string, object> Form { get; set; }
        
        public IDictionary<string, string> Headers { get; set; }

        public string RawData { get; set; }
        
        public string GetHeaderValue(string key) {
            var kvp = Headers?.FirstOrDefault(h => String.Equals(h.Key, key, StringComparison.OrdinalIgnoreCase));
            return kvp?.Value;
        }
    }
}
