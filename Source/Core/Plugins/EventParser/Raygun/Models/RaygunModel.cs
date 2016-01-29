using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class RaygunModel {
        [JsonProperty("occurredOn")]
        public DateTime OccurredOn { get; set; }

        [JsonProperty("details")]
        public Details Details { get; set; }
    }
}
