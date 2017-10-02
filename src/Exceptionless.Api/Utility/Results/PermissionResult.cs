using System;
using Microsoft.AspNetCore.Http;

namespace Exceptionless.Api.Utility.Results {
    public class PermissionResult {
        public bool Allowed { get; set; }
        public string Id { get; set; }
        public string Message { get; set; }

        public int StatusCode { get; set; }

        public static PermissionResult Allow = new PermissionResult { Allowed = true, StatusCode = StatusCodes.Status200OK };

        public static PermissionResult Deny = new PermissionResult { Allowed = false, StatusCode = StatusCodes.Status400BadRequest };

        public static PermissionResult DenyWithNotFound(string id = null) {
            return new PermissionResult {
                Allowed = false,
                Id = id,
                StatusCode = StatusCodes.Status404NotFound
            };
        }

        public static PermissionResult DenyWithMessage(string message, string id = null) {
            return new PermissionResult {
                Allowed = false,
                Id = id,
                Message = message,
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        public static PermissionResult DenyWithStatus(int statusCode, string message = null, string id = null) {
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
                StatusCode = StatusCodes.Status426UpgradeRequired
            };
        }


        public static PermissionResult DenyWithPNotImplemented(string message, string id = null) {
            return new PermissionResult {
                Allowed = false,
                Id = id,
                Message = message,
                StatusCode = StatusCodes.Status501NotImplemented
            };
        }
    }
}