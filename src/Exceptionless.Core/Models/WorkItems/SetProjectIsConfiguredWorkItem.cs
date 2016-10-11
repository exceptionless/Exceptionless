using System;

namespace Exceptionless.Core.Models.WorkItems {
    public class SetProjectIsConfiguredWorkItem {
        public string ProjectId { get; set; }
        public bool IsConfigured { get; set; }
    }
}