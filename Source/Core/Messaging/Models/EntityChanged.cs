using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Messaging.Models {
    public class EntityChanged {
        public EntityChanged() {
            Ids = new List<string>();
        }

        public string Type { get; set; }
        public ICollection<string> Ids { get; set; }
        public string OrganizationId { get; set; }
        public ChangeType ChangeType { get; set; }
    }
}
