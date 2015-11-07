using System;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models {
    public class WebHook : IOwnedByOrganizationAndProjectWithIdentity, IHaveCreatedDate {
        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string Url { get; set; }
        public string[] EventTypes { get; set; }

        /// <summary>
        /// The schema version that should be used.
        /// </summary>
        public Version Version { get; set; }

        public DateTime CreatedUtc { get; set; }
    }
}
