using System;
using System.Threading.Tasks;
using Exceptionless.Web.Extensions;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Foundatio.Serializer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Web.Utility {
    public sealed class ProjectConfigMiddleware {
        private readonly IProjectRepository _projectRepository;
        private readonly ITextSerializer _serializer;
        private readonly ILogger _logger;
        private readonly RequestDelegate _next;
        private static readonly PathString _v1path = new PathString("/api/v1/projects/config");
        private static readonly PathString _v2path = new PathString("/api/v2/projects/config");
        private static readonly PathString _getVerb = new PathString("GET");

        public ProjectConfigMiddleware(RequestDelegate next, IProjectRepository projectRepository, ITextSerializer serializer, ILogger<ProjectConfigMiddleware> logger) {
            _next = next;
            _projectRepository = projectRepository;
            _serializer = serializer;
            _logger = logger;
        }

        private bool IsProjectConfigRoute(HttpContext context) {
            if (!context.Request.Method.Equals(HttpMethods.Get, StringComparison.Ordinal))
                return false;

            if (context.Request.Path.StartsWithSegments(_v2path, StringComparison.Ordinal)
                || context.Request.Path.StartsWithSegments(_v1path, StringComparison.Ordinal))
                return true;

            return false;
        }

        public async Task Invoke(HttpContext context) {
            if (!IsProjectConfigRoute(context)) {
                await _next(context);
                return;
            }

            string projectId = context.Request.GetDefaultProjectId();
            if (String.IsNullOrEmpty(projectId)) {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var project = await _projectRepository.GetByIdAsync(projectId, o => o.Cache());
            if (project == null) {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (context.Request.Query.TryGetValue("v", out var v) && Int32.TryParse(v, out int version) && version == project.Configuration.Version) {
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                return;
            }

            string json = _serializer.SerializeToString(project.Configuration);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength = json.Length;
            await context.Response.WriteAsync(json);
        }
    }
}