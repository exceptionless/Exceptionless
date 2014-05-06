using System;
using System.Web.Http;

namespace Exceptionless.Api.Controllers {
    public class PermissionResult {
        public bool Allowed { get; set; }

        public IHttpActionResult HttpActionResult { get; set; }

        public static PermissionResult Allow = new PermissionResult { Allowed = true };

        public static PermissionResult Deny = new PermissionResult { Allowed = false };

        public static PermissionResult DenyWithResult(IHttpActionResult result) {
            return new PermissionResult {
                Allowed = false,
                HttpActionResult = result
            };
        }
    }
}