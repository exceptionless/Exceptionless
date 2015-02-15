using System;

namespace Exceptionless.Core.Messaging.Models {
    public class PlanOverage {
        public string OrganizationId { get; set; }
        public bool IsHourly { get; set; }
    }
}
