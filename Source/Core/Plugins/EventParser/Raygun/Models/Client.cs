using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class Client {
        public string Name { get; set; }

        public string Version { get; set; }

        public string ClientUrl { get; set; }
    }
}
