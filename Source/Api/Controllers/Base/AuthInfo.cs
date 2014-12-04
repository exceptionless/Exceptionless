using System;

namespace Exceptionless.Api.Controllers {
    public class AuthInfo {
        public string ClientId { get; set; }
        public string Code { get; set; }
        public string RedirectUri { get; set; }
    }
}