using System;

namespace Exceptionless.Api.Models {
    public class ChangePasswordModel {
        public string CurrentPassword { get; set; }
        public string Password { get; set; }
    }
}