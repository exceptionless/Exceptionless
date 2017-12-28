using System;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Serializer;
using Foundatio.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Api.Utility {
    public sealed class RecordSessionHeartbeatMiddleware {
        private readonly IProjectRepository _projectRepository;
        private readonly ITextSerializer _serializer;
        private readonly ICacheClient _cache;
        private readonly ILogger _logger;
        private readonly RequestDelegate _next;

        public RecordSessionHeartbeatMiddleware(RequestDelegate next, IProjectRepository projectRepository, ITextSerializer serializer, ICacheClient cache, ILogger<ProjectConfigMiddleware> logger) {
            _next = next;
            _projectRepository = projectRepository;
            _serializer = serializer;
            _cache = cache;
            _logger = logger;
        }

        private bool IsHeartbeatRoute(HttpContext context) {
            string method = context.Request.Method;
            if (!String.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                return false;

            string absolutePath = context.Request.Path.Value;
            return String.Equals(absolutePath, "/session/heartbeat", StringComparison.OrdinalIgnoreCase);
        }

        public async Task Invoke(HttpContext context) {
            if (!IsHeartbeatRoute(context)) {
                await _next(context);
                return;
            }

            string projectId = context.Request.GetDefaultProjectId();
            if (String.IsNullOrEmpty(projectId)) {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (Settings.Current.EventSubmissionDisabled || !context.Request.Query.TryGetValue("id", out var id) || String.IsNullOrEmpty(id)) {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return;
            }

            string identityHash = id.ToSHA1();
            string heartbeatCacheKey = String.Concat("Project:", projectId, ":heartbeat:", identityHash);
            bool close = context.Request.Query.TryGetValue("close", out var c) && Boolean.TryParse(c, out var closed) && closed;
            try {
                await Task.WhenAll(
                    _cache.SetAsync(heartbeatCacheKey, SystemClock.UtcNow, TimeSpan.FromHours(2)),
                    close ? _cache.SetAsync(String.Concat(heartbeatCacheKey, "-close"), true, TimeSpan.FromHours(2)) : Task.CompletedTask
                );
            } catch (Exception ex) {
                if (projectId != Settings.Current.InternalProjectId) {
                    using (_logger.BeginScope(new ExceptionlessState().Project(projectId).Property("Id", id).Property("Close", close).SetHttpContext(context)))
                        _logger.LogError(ex, "Error enqueuing session heartbeat.");
                }

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
        }
    }
}