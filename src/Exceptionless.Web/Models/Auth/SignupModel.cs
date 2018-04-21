using System;

namespace Exceptionless.Api.Models {
    public class SignupModel : LoginModel {
        public string Name { get; set; }
    }
}