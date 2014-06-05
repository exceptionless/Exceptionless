using System;
using System.Collections.Generic;

namespace Exceptionless.Models.Admin {
    public class Token : IIdentity, IOwnedByOrganization, IOwnedByProject {
        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string UserId { get; set; }
        public string ApplicationId { get; set; }
        public string DefaultProjectId { get; set; }
        public string Refresh { get; set; }
        public string Type { get; set; }
        public HashSet<string> Scopes { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
    }
}
