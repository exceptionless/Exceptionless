using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class StackTrace {
        [JsonProperty("lineNumber")]
        public int LineNumber { get; set; }

        [JsonProperty("className")]
        public string ClassName { get; set; }

        [JsonProperty("columnNumber")]
        public int ColumnNumber { get; set; }

        [JsonProperty("fileName")]
        public string FileName { get; set; }

        [JsonProperty("methodName")]
        public string MethodName { get; set; }
    }
}
