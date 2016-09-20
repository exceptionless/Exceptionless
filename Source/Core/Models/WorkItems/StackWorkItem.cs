using System;

namespace Exceptionless.Core.Models.WorkItems {
    public class StackWorkItem {
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string StackId { get; set; }
        public bool Delete { get; set; }
        public bool UpdateIsFixed { get; set; }
        public bool IsFixed { get; set; }
        public bool UpdateIsHidden { get; set; }
        public bool IsHidden { get; set; }
    }
}