using System;

namespace Exceptionless.Core.Models.WorkItems {
    public class RemoveProjectWorkItem {
        public string ProjectId { get; set; }
        public bool Reset { get; set; }
    }
}