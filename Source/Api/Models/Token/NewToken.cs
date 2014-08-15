using System;
using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Api.Models {
    public class NewToken : IOwnedByOrganization, IOwnedByProject {
        public NewToken() {
            Scopes = new HashSet<string>();
        }

        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string ApplicationId { get; set; }
        public string DefaultProjectId { get; set; }
        public HashSet<string> Scopes { get; set; }
        public string Notes { get; set; }
    }
}