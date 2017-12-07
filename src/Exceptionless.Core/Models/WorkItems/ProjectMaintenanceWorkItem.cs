using System;

namespace Exceptionless.Core.Models.WorkItems {
    public class ProjectMaintenanceWorkItem {
        public bool UpdateDefaultBotList { get; set; }
        public bool IncrementConfigurationVersion { get; set; }
        public bool RemoveOldUsageStats { get; set; }
    }
}