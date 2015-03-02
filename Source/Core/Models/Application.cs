using System;

namespace Exceptionless.Core.Models.Admin {
    public class Application : IOwnedByOrganizationWithIdentity {
        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public string Secret { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string Description { get; set; }
        public string CallbackUrl { get; set; }
        public string ImageUrl { get; set; }
    }
}
