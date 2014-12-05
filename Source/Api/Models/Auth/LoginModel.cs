using System;

namespace Exceptionless.Api.Models {
    public class LoginModel {
        public string Email { get; set; }
        public string Password { get; set; }
        public bool Remember { get; set; }
    }
}