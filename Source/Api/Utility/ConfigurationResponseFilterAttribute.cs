using System;
using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;

namespace Exceptionless.Api.Utility {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class ConfigurationResponseFilterAttribute : ActionFilterAttribute {
        [Inject]
        public IProjectRepository ProjectRepository { get; set; }

        public override void OnActionExecuted(HttpActionExecutedContext context) {
            if (context == null)
                throw new ArgumentNullException("context");

            if (context.Response == null || context.Response.StatusCode != HttpStatusCode.Accepted)
                return;

            var ctx = context.Request.GetOwinContext();
            if (ctx == null || ctx.Request == null || ctx.Request.User == null)
                return;
            
            string projectId = ctx.Request.User.GetProjectId();
            if (String.IsNullOrEmpty(projectId))
                return;

            var project = ProjectRepository.GetById(projectId, true);
            if (project == null)
                return;

            string headerName = ExceptionlessHeaders.ConfigurationVersion;
            if (context.Request.RequestUri.AbsolutePath.StartsWith("/api/v1"))
                headerName = ExceptionlessHeaders.LegacyConfigurationVersion;

            // add the current configuration version to the response headers so the client will know if it should update its config.
            context.Response.Headers.Add(headerName, project.Configuration.Version.ToString());
        }
    }
}