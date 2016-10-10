using System;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models {
    public class Application : IOwnedByOrganizationWithIdentity, IHaveDates {
        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public string Secret { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string Description { get; set; }
        public string CallbackUrl { get; set; }
        public string ImageUrl { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ModifiedUtc { get; set; }
        DateTime IHaveDates.UpdatedUtc { get { return ModifiedUtc; } set { ModifiedUtc = value; } }
    }
}
