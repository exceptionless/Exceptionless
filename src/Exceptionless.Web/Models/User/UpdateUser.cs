using System;

namespace Exceptionless.Api.Models {
    public class UpdateUser {
        public string FullName { get; set; }
        public bool EmailNotificationsEnabled { get; set; }
    }
}