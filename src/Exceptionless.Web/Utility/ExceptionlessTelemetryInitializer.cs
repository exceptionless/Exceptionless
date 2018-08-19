using Exceptionless.Web.Extensions;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;

namespace Exceptionless.Web.Utility {
    public class ExceptionlessTelemetryInitializer : ITelemetryInitializer {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ExceptionlessTelemetryInitializer(IHttpContextAccessor httpContextAccessor) {
            _httpContextAccessor = httpContextAccessor;
        }

        public void Initialize(ITelemetry telemetry) {
            if (_httpContextAccessor.HttpContext == null)
                return;

            telemetry.Context.User.UserAgent = _httpContextAccessor.HttpContext.Request.Headers["User-Agent"];
            telemetry.Context.User.AccountId = _httpContextAccessor.HttpContext.Request.GetDefaultProjectId();
            telemetry.Context.User.Id = _httpContextAccessor.HttpContext.Items["ApiKey"]?.ToString();
            telemetry.Context.Location.Ip = _httpContextAccessor.HttpContext.Request.GetClientIpAddress();
        }
    }
}
