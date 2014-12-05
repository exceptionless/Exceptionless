using System;

namespace Exceptionless.Api.Models {
    public class ExternalAuthInfo {
        public string ClientId { get; set; }
        public string Code { get; set; }
        public string RedirectUri { get; set; }
        public string InviteToken { get; set; }
    }
}