using System;

namespace Exceptionless.Core.Messaging.Models {
    public class EntityChanged {
        public string Type { get; set; }
        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public ChangeType ChangeType { get; set; }
    }
}
