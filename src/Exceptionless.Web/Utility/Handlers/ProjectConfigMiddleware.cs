using System;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories;
using Exceptionless.Web.Extensions;
using Foundatio.Repositories;
using Foundatio.Serializer;
using Microsoft.AspNetCore.Http;

namespace Exceptionless.Web.Utility {
    public sealed class ProjectConfigMiddleware {
        private readonly IProjectRepository _projectRepository;
        private readonly ITextSerializer _serializer;
        private readonly RequestDelegate _next;
        private static readonly PathString _v1Path = new PathString("/api/v1/project/config");
        private static readonly PathString _v2Path = new PathString("/api/v2/projects/config");

        public ProjectConfigMiddleware(RequestDelegate next, IProjectRepository projectRepository, ITextSerializer serializer) {
            _next = next;
            _projectRepository = projectRepository;
            _serializer = serializer;
        }

        private bool IsProjectConfigRoute(HttpContext context) {
            if (!context.Request.Method.Equals(HttpMethods.Get, StringComparison.Ordinal))
                return false;

            if (context.Request.Path.StartsWithSegments(_v2Path, StringComparison.Ordinal)
                || context.Request.Path.StartsWithSegments(_v1Path, StringComparison.Ordinal))
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

            var project = await _projectRepository.GetAsync(projectId, o => o.Cache());
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