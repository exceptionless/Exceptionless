using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class Error {
        [JsonProperty("innerError")]
        public Error InnerError { get; set; }

        [JsonProperty("data")]
        public IDictionary Data { get; set; }

        [JsonProperty("className")]
        public string ClassName { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("stackTrace")]
        public IList<StackTrace> StackTrace { get; set; }
    }
}
