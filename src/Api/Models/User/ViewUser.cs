using System;
using System.Collections.Generic;
using Foundatio.Repositories.Models;

namespace Exceptionless.Api.Models {
    public class ViewUser : IIdentity {
        public string Id { get; set; }
        public ICollection<string> OrganizationIds { get; set; }
        public string FullName { get; set; }
        public string EmailAddress { get; set; }
        public bool EmailNotificationsEnabled { get; set; }
        public bool IsEmailAddressVerified { get; set; }
        public bool IsActive { get; set; }
        public bool IsInvite { get; set; }
        public ICollection<string> Roles { get; set; }
    }
}