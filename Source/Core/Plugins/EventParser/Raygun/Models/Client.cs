using System;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class Client {
        public string Name { get; set; }

        public string Version { get; set; }

        public string ClientUrl { get; set; }
    }
}
