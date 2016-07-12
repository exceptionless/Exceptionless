using System;

namespace Exceptionless.Core.Models.WorkItems {
    public class OrganizationNotificationWorkItem {
        public string OrganizationId { get; set; }
        public bool IsOverHourlyLimit { get; set; }
        public bool IsOverMonthlyLimit { get; set; }
    }
}