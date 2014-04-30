using System;
using System.Web.Http;

namespace Exceptionless.Core.Controllers {
    public class PermissionResult {
        public bool Allowed { get; set; }

        public IHttpActionResult HttpActionResult { get; set; }

        public static PermissionResult Allow = new PermissionResult { Allowed = true };

        public static PermissionResult Deny = new PermissionResult { Allowed = false };
     }
}