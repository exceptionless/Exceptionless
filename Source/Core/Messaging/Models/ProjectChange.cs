using System;

namespace Exceptionless.Core.Messaging.Models {
    public class ProjectChange {
        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public bool IsNew { get; set; }
    }
}
