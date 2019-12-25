using System;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models {
    public class WebHook : IOwnedByOrganizationAndProjectWithIdentity, IHaveCreatedDate {
        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string Url { get; set; }
        public string[] EventTypes { get; set; }
        
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// The schema version that should be used.
        /// </summary>
        public string Version { get; set; }

        public DateTime CreatedUtc { get; set; }
        
        public static class KnownVersions {
            public const string Version1 = "v1";
            public const string Version2 = "v2";
        }
    }
}
