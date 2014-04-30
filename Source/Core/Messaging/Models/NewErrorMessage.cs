using System;

namespace Exceptionless.Core.Messaging.Models {
    public class NewErrorMessage {
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string StackId { get; set; }
        public bool IsHidden { get; set; }
        public bool IsFixed { get; set; }
        public bool IsNotFound { get; set; }
    }
}
