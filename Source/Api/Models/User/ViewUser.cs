using System;
using Exceptionless.Models;

namespace Exceptionless.Api.Models.User {
    public class ViewUser : IIdentity {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string EmailAddress { get; set; }
        public bool EmailNotificationsEnabled { get; set; }
        public bool IsEmailAddressVerified { get; set; }
        public bool IsInvite { get; set; }
        public bool HasAdminRole { get; set; }
    }
}