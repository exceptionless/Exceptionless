using System;
using Exceptionless.Models;

namespace Exceptionless.Api.Models {
    public class NewWebHook : IOwnedByProject, IOwnedByOrganization {
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string Url { get; set; }
        public string[] EventTypes { get; set; }

        /// <summary>
        /// The schema version that should be used.
        /// </summary>
        public Version Version { get; set; }
    }
}