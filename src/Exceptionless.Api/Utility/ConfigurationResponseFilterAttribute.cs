using System;
using Exceptionless.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Exceptionless.Api.Utility {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class ConfigurationResponseFilterAttribute : ActionFilterAttribute {
        public override void OnActionExecuted(ActionExecutedContext context) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.HttpContext.Response == null || (context.HttpContext.Response.StatusCode != StatusCodes.Status200OK && context.HttpContext.Response.StatusCode != StatusCodes.Status202Accepted))
                return;

            var project = context.HttpContext.Request?.GetProject();
            if (project == null)
                return;

            string headerName = Headers.ConfigurationVersion;
            if (context.HttpContext.Request.Path.Value.StartsWith("/api/v1"))
                headerName = Headers.LegacyConfigurationVersion;

            // add the current configuration version to the response headers so the client will know if it should update its config.
            context.HttpContext.Response.Headers.Add(headerName, project.Configuration.Version.ToString());
        }
    }
}