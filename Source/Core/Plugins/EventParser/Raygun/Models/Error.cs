using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class Error {
        public Error InnerError { get; set; }

        public IDictionary Data { get; set; }

        public string ClassName { get; set; }

        public string Message { get; set; }

        public IList<StackTrace> StackTrace { get; set; }
    }
}
