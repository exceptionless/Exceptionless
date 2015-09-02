using System;

namespace Exceptionless.Api.Models {
    public class ResetPasswordModel {
        public string PasswordResetToken { get; set; }
        public string Password { get; set; }
    }
}