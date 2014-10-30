using System;
using System.Net;

namespace Exceptionless.Api.Controllers {
    public class PermissionResult {
        public bool Allowed { get; set; }

        public string Id { get; set; }

        public string Message { get; set; }

        public HttpStatusCode StatusCode { get; set; }

        public static PermissionResult Allow = new PermissionResult { Allowed = true, StatusCode = HttpStatusCode.OK };

        public static PermissionResult Deny = new PermissionResult { Allowed = false, StatusCode = HttpStatusCode.BadRequest };

        public static PermissionResult DenyWithNotFound(string id = null) {
            return new PermissionResult {
                Allowed = false,
                Id = id,
                StatusCode = HttpStatusCode.NotFound
            };
        }

        public static PermissionResult DenyWithMessage(string message, string id = null) {
            return new PermissionResult {
                Allowed = false,
                Id = id,
                Message = message,
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        public static PermissionResult DenyWithStatus(HttpStatusCode statusCode, string message = null, string id = null) {
            return new PermissionResult {
                Allowed = false,
                Id = id,
                Message = message,
                StatusCode = statusCode
            };
        }

        public static PermissionResult DenyWithPlanLimitReached(string message, string id = null) {
            return new PermissionResult {
                Allowed = false,
                Id = id,
                Message = message,
                StatusCode = HttpStatusCode.UpgradeRequired
            };
        }

    }
}