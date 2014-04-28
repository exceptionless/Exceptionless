using System;
using System.Web.Http;
using Exceptionless.Core.Web.Results;

namespace Exceptionless.Core.Extensions {
    public static class ApiControllerExtensions {
        public static PlanLimitReachedActionResult PlanLimitReached(this ApiController controller, string message) {
            return new PlanLimitReachedActionResult(message, controller.Request);
        }

        public static NotImplementedActionResult NotImplemented(this ApiController controller, string message) {
            return new NotImplementedActionResult(message, controller.Request);
        }
    }
}