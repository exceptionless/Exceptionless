using System;

namespace Exceptionless.Core.Messaging.Models {
    public class StackUpdated {
        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public bool IsHidden { get; set; }
        public bool IsFixed { get; set; }
        public bool Is404 { get; set; }
    }
}
