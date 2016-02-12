using System;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class RaygunModel {
        public DateTime OccurredOn { get; set; }

        public Details Details { get; set; }
    }
}
