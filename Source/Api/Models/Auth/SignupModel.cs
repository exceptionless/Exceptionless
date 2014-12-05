using System;

namespace Exceptionless.Api.Models {
    public class SignupModel {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string InviteToken { get; set; }
    }
}