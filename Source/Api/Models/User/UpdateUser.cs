using System;

namespace Exceptionless.Api.Models.User {
    public class UpdateUser {
        public string FullName { get; set; }
        public bool EmailNotificationsEnabled { get; set; }
    }
}