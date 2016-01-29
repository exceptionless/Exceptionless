using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class Details {

        public string MachineName { get; set; }

        public string GroupingKey { get; set; }

        public string Version { get; set; }

        public Client Client { get; set; }

        public Error Error { get; set; }

        public Environment Environment { get; set; }

        public IList<string> Tags { get; set; }

        public IDictionary UserCustomData { get; set; }

        public Request Request { get; set; }

        public Response Response { get; set; }

        public User User { get; set; }
    }
}
