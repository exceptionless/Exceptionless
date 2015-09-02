using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;

namespace Exceptionless.Api.Models {
    public class ViewToken : IIdentity {
        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string UserId { get; set; }
        public string ApplicationId { get; set; }
        public string DefaultProjectId { get; set; }
        public HashSet<string> Scopes { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ModifiedUtc { get; set; }
    }
}
