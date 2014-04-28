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

        public static int GetPageSize(this ApiController controller, int pageSize) {
            if (pageSize < 1)
                pageSize = 10;
            else if (pageSize > 100)
                pageSize = 100;

            return pageSize;
        }

        public static int GetSkip(this ApiController controller, int currentPage, int pageSize) {
            int skip = (currentPage - 1) * pageSize;
            if (skip < 0)
                skip = 0;

            return skip;
        }

        public static Tuple<DateTime, DateTime> GetDateRange(this ApiController controller, DateTime? starTime, DateTime? endTime) {
            if (starTime == null)
                starTime = DateTime.MinValue;

            if (endTime == null)
                endTime = DateTime.MaxValue;

            return starTime < endTime 
                ? new Tuple<DateTime, DateTime>(starTime.Value, endTime.Value) 
                : new Tuple<DateTime, DateTime>(endTime.Value, starTime.Value);
        }
    }
}