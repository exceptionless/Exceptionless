using System;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class Response {
        public int StatusCode { get; set; }

        public string StatusDescription { get; set; }
    }
}
