using Exceptionless.Web.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Services;

namespace Exceptionless.Web.Utility;

public sealed class OverageMiddleware {
    private readonly UsageService _usageService;
    private readonly AppOptions _appOptions;
    private readonly ILogger _logger;
    private readonly RequestDelegate _next;

    public OverageMiddleware(RequestDelegate next, UsageService usageService, AppOptions appOptions, ILogger<OverageMiddleware> logger) {
        _next = next;
        _usageService = usageService;
        _appOptions = appOptions;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context) {
        if (!context.Request.IsEventPost()) {
            await _next(context);
            return;
        }

        if (_appOptions.EventSubmissionDisabled) {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        bool tooBig = false;
        if (String.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase) && context.Request.Headers != null) {
            if (context.Request.Headers.ContentLength.HasValue && context.Request.Headers.ContentLength.Value <= 0) {
                AppDiagnostics.PostsBlocked.Add(1);
                context.Response.StatusCode = StatusCodes.Status411LengthRequired;
                return;
            }

            long size = context.Request.Headers.ContentLength.GetValueOrDefault();
            if (size > 0)
                AppDiagnostics.PostsSize.Record(size);

            if (size > _appOptions.MaximumEventPostSize) {
                if (_logger.IsEnabled(LogLevel.Warning)) {
                    using (_logger.BeginScope(new ExceptionlessState().Value(size).Tag(context.Request.Headers.TryGetAndReturn(Headers.ContentEncoding))))
                        _logger.SubmissionTooLarge(size);
                }

                AppDiagnostics.PostsDiscarded.Add(1);
                tooBig = true;
            }
        }

        string organizationId = context.Request.GetDefaultOrganizationId();

        // block large submissions, client should break them up or remove some of the data.
        if (tooBig) {
            string projectId = context.Request.GetDefaultProjectId();
            await _usageService.IncrementTooBigAsync(organizationId, projectId).AnyContext();
            context.Response.StatusCode = StatusCodes.Status413RequestEntityTooLarge;
            return;
        }

        int eventsLeft = await _usageService.GetEventsLeftAsync(organizationId).AnyContext();
        if (eventsLeft <= 0) {
            AppDiagnostics.PostsBlocked.Add(1);
            string projectId = context.Request.GetDefaultProjectId();
            await _usageService.IncrementDiscardedAsync(organizationId, projectId);
            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            return;
        }

        await _next(context);
    }
}
