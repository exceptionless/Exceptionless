using System;

namespace Exceptionless.Api.Models.User {
    public class UserModel {
        public string Id { get; set; }
        public string EmailAddress { get; set; }
        public string FullName { get; set; }
        public bool IsEmailAddressVerified { get; set; }
        public bool IsInvite { get; set; }
        public bool HasAdminRole { get; set; }
    }
}