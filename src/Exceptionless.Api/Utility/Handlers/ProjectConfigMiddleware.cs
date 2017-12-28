using System;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Foundatio.Serializer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Api.Utility {
    public sealed class ProjectConfigMiddleware {
        private readonly IProjectRepository _projectRepository;
        private readonly ITextSerializer _serializer;
        private readonly ILogger _logger;
        private readonly RequestDelegate _next;

        public ProjectConfigMiddleware(RequestDelegate next, IProjectRepository projectRepository, ITextSerializer serializer, ILogger<ProjectConfigMiddleware> logger) {
            _next = next;
            _projectRepository = projectRepository;
            _serializer = serializer;
            _logger = logger;
        }

        private bool IsProjectConfigRoute(HttpContext context) {
            string method = context.Request.Method;
            if (!String.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                return false;

            string absolutePath = context.Request.Path.Value;
            return String.Equals(absolutePath, "/api/v2/projects/config", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(absolutePath, "/api/v1/project/config", StringComparison.OrdinalIgnoreCase);
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

            if (context.Request.Query.TryGetValue("v", out var v) && Int32.TryParse(v, out var version) && version == project.Configuration.Version) {
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