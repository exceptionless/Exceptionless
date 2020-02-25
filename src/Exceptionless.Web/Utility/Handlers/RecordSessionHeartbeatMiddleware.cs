using System;
using System.Threading.Tasks;
using Exceptionless.Web.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Foundatio.Caching;
using Foundatio.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Web.Utility {
    public sealed class RecordSessionHeartbeatMiddleware {
        private readonly ICacheClient _cache;
        private readonly AppOptions _appOptions;
        private readonly ILogger _logger;
        private readonly RequestDelegate _next;
        private static readonly PathString _heartbeatPath = new PathString("/api/v2/events/session/heartbeat");

        public RecordSessionHeartbeatMiddleware(RequestDelegate next, ICacheClient cache, AppOptions appOptions, ILogger<ProjectConfigMiddleware> logger) {
            _next = next;
            _cache = cache;
            _appOptions = appOptions;
            _logger = logger;
        }

        private bool IsHeartbeatRoute(HttpContext context) {
            if (!context.Request.Method.Equals(HttpMethods.Get, StringComparison.Ordinal))
                return false;

            return context.Request.Path.StartsWithSegments(_heartbeatPath, StringComparison.Ordinal);
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

            if (_appOptions.EventSubmissionDisabled || !context.Request.Query.TryGetValue("id", out var id) || String.IsNullOrEmpty(id)) {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return;
            }

            string identityHash = id.ToSHA1();
            string heartbeatCacheKey = String.Concat("Project:", projectId, ":heartbeat:", identityHash);
            bool close = context.Request.Query.TryGetValue("close", out var c) && Boolean.TryParse(c, out bool closed) && closed;
            try {
                await Task.WhenAll(
                    _cache.SetAsync(heartbeatCacheKey, SystemClock.UtcNow, TimeSpan.FromHours(2)),
                    close ? _cache.SetAsync(String.Concat(heartbeatCacheKey, "-close"), true, TimeSpan.FromHours(2)) : Task.CompletedTask
                );
            } catch (Exception ex) {
                if (projectId != _appOptions.InternalProjectId) {
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