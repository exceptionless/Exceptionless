using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class Request {
        public string HostName { get; set; }

        public string Url { get; set; }

        public string HttpMethod { get; set; }

        public string IPAddress { get; set; }

        public IDictionary<string, string> QueryString { get; set; }

        public IList Cookies { get; set; }

        public IDictionary<string, string> Form { get; set; }

        public IDictionary<string, string> Data { get; set; }

        public IDictionary<string, string> Headers { get; set; }

        public string RawData { get; set; }

        public string GetDataValue(string key) {
            var isValueExists = this.Data.Any(x => x.Key.ToUpperInvariant() == key);

            if (isValueExists) {
                return this.Data.Single(x => x.Key.ToUpperInvariant() == key).Value;
            }

            return null;
        }

        public string GetHeaderValue(string key) {
            var isValueExists = this.Headers.Any(x => x.Key.ToUpperInvariant() == key);

            if (isValueExists) {
                return this.Headers.Single(x => x.Key.ToUpperInvariant() == key).Value;
            }

            return null;
        }
    }
}
