using System;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Exceptionless.Api.Security {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireHttpsAttribute : Attribute, IAuthorizationFilter, IOrderedFilter {
        public bool Permanent { get; set; }
        public bool IgnoreLocalRequests { get; set; }
        public int Order { get; set; }

        public virtual void OnAuthorization(AuthorizationFilterContext filterContext) {
            if (filterContext == null)
                throw new ArgumentNullException(nameof(filterContext));

            var logger = filterContext.HttpContext.RequestServices.GetRequiredService<ILogger<RequireHttpsAttribute>>();
            if (IsSecure(filterContext.HttpContext.Request, logger))
                return;

            if (IgnoreLocalRequests && IsLocal(filterContext.HttpContext.Request))
                return;

            HandleNonHttpsRequest(filterContext);
        }

        private bool IsSecure(HttpRequest request, ILogger logger) {
            bool isLogTraceEnabled = logger.IsEnabled(LogLevel.Trace);
            if (request.Headers.TryGetValue("X-Forwarded-Proto", out StringValues value)) {
                if (isLogTraceEnabled) logger.LogTrace("X-Forwarded-Proto header present with value: {Value}", value.FirstOrDefault());
                if (value.FirstOrDefault() == Uri.UriSchemeHttps)
                    return true;
            }

            if (isLogTraceEnabled) logger.LogTrace("X-Forwarded-Proto not present");
            return request.IsHttps;
        }

        private const string NullIpAddress = "::1";

        public bool IsLocal(HttpRequest request) {
            if (request.Host.Host.Contains("localtest.me") ||
                request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                return true;

            var connection = request.HttpContext.Connection;

            if (IsSet(connection.RemoteIpAddress)) {
                return IsSet(connection.LocalIpAddress)
                    ? connection.RemoteIpAddress.Equals(connection.LocalIpAddress)
                    : IPAddress.IsLoopback(connection.RemoteIpAddress);
            }

            return true;
        }

        private bool IsSet(IPAddress address) {
            return address != null && address.ToString() != NullIpAddress;
        }

        protected virtual void HandleNonHttpsRequest(AuthorizationFilterContext filterContext) {
            var logger = filterContext.HttpContext.RequestServices.GetRequiredService<ILogger<RequireHttpsAttribute>>();
            bool isLogInformationEnabled = logger.IsEnabled(LogLevel.Information);

            // only redirect for GET requests, otherwise the browser might not propagate the verb and request
            // body correctly.
            if (!String.Equals(filterContext.HttpContext.Request.Method, "GET", StringComparison.OrdinalIgnoreCase)) {
                if(isLogInformationEnabled) logger.LogInformation("Unable to redirect to SSL for non-GET requests");
                filterContext.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            } else {
                var optionsAccessor = filterContext.HttpContext.RequestServices.GetRequiredService<IOptions<MvcOptions>>();

                var request = filterContext.HttpContext.Request;

                var host = request.Host;
                if (optionsAccessor.Value.SslPort.HasValue && optionsAccessor.Value.SslPort > 0) {
                    // a specific SSL port is specified
                    host = new HostString(host.Host, optionsAccessor.Value.SslPort.Value);
                } else {
                    // clear the port
                    host = new HostString(host.Host);
                }

                string newUrl = String.Concat(
                    "https://",
                    host.ToUriComponent(),
                    request.PathBase.ToUriComponent(),
                    request.Path.ToUriComponent(),
                    request.QueryString.ToUriComponent());

                if (isLogInformationEnabled) logger.LogInformation("Redirecting non-SSL request to {Url}", newUrl);
                // redirect to HTTPS version of page
                filterContext.Result = new RedirectResult(newUrl, Permanent);
            }
        }
    }
}