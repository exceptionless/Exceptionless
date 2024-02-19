using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Web.Extensions;
using Foundatio.Repositories;

namespace Exceptionless.Web.Utility;

public sealed class OverageMiddleware
{
    private readonly UsageService _usageService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly AppOptions _appOptions;
    private readonly ILogger _logger;
    private readonly RequestDelegate _next;

    public OverageMiddleware(RequestDelegate next, UsageService usageService, IOrganizationRepository organizationRepository, AppOptions appOptions, ILogger<OverageMiddleware> logger)
    {
        _next = next;
        _usageService = usageService;
        _organizationRepository = organizationRepository;
        _appOptions = appOptions;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        if (!context.Request.IsEventPost())
        {
            await _next(context);
            return;
        }

        string? organizationId = context.Request.GetDefaultOrganizationId();
        if (String.IsNullOrEmpty(organizationId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (_appOptions.EventSubmissionDisabled)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        bool tooBig = false;
        if (String.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase) && context.Request.Headers is not null)
        {
            if (context.Request.Headers.ContentLength.HasValue && context.Request.Headers.ContentLength.Value <= 0)
            {
                AppDiagnostics.PostsBlocked.Add(1);
                context.Response.StatusCode = StatusCodes.Status411LengthRequired;
                return;
            }

            long size = context.Request.Headers.ContentLength.GetValueOrDefault();
            if (size > 0)
                AppDiagnostics.PostsSize.Record(size);

            if (size > _appOptions.MaximumEventPostSize)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    using (_logger.BeginScope(new ExceptionlessState().Value(size).Tag(context.Request.Headers.TryGetAndReturn(Headers.ContentEncoding))))
                        _logger.SubmissionTooLarge(size);
                }

                tooBig = true;
            }
        }


        // block large submissions, client should break them up or remove some of the data.
        if (tooBig)
        {
            string? projectId = context.Request.GetDefaultProjectId();
            await _usageService.IncrementTooBigAsync(organizationId, projectId);
            context.Response.StatusCode = StatusCodes.Status413RequestEntityTooLarge;
            return;
        }

        int eventsLeft = await _usageService.GetEventsLeftAsync(organizationId);
        if (eventsLeft <= 0)
        {
            string? projectId = context.Request.GetDefaultProjectId();
            await _usageService.IncrementBlockedAsync(organizationId, projectId);
            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            return;
        }

        // if user auth, check to see if the org is suspended
        // api tokens are marked as suspended immediately
        if (context.Request.GetAuthType() == AuthType.User)
        {
            AppDiagnostics.PostsBlocked.Add(1);
            var organization = await _organizationRepository.GetByIdAsync(organizationId, o => o.Cache());
            if (organization.IsSuspended)
            {
                context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                return;
            }
        }

        await _next(context);
    }
}
