using System;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class RaygunModel {
        public DateTimeOffset OccurredOn { get; set; }

        public Details Details { get; set; }
    }
}
