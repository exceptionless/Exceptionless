using System;

namespace Exceptionless.Core.Messaging.Models {
    public class EntityChanged {
        public string Type { get; set; }
        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public EntityChangeType ChangeType { get; set; }
    }

    public enum EntityChangeType {
        Added,
        Saved,
        Removed,
        RemovedAll,
        UpdatedAll
    }
}
