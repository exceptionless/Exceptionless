using System;

namespace Exceptionless.Core.Models.WorkItems {
    public class OrganizationMaintenanceWorkItem {
        public bool UpgradePlans { get; set; }
        public bool RemoveOldUsageStats { get; set; }
    }
}