using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Filters;
using Exceptionless.Api.Extensions;

namespace Exceptionless.Api.Utility {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class ConfigurationResponseFilterAttribute : ActionFilterAttribute {
        public override Task OnActionExecutedAsync(HttpActionExecutedContext context, CancellationToken cancellationToken) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.Response == null || context.Response.StatusCode != HttpStatusCode.Accepted)
                return Task.CompletedTask;

            var project = context.Request?.GetProject();
            if (project == null)
                return Task.CompletedTask;

            string headerName = ExceptionlessHeaders.ConfigurationVersion;
            if (context.Request.RequestUri.AbsolutePath.StartsWith("/api/v1"))
                headerName = ExceptionlessHeaders.LegacyConfigurationVersion;

            // add the current configuration version to the response headers so the client will know if it should update its config.
            context.Response.Headers.Add(headerName, project.Configuration.Version.ToString());

            return Task.CompletedTask;
        }
    }
}