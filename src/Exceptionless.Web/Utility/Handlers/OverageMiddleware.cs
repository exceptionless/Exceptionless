using Exceptionless.Web.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Foundatio.Repositories;

namespace Exceptionless.Web.Utility;

public sealed class OverageMiddleware {
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly UsageService _usageService;
    private readonly AppOptions _appOptions;
    private readonly ILogger _logger;
    private readonly RequestDelegate _next;

    public OverageMiddleware(RequestDelegate next, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, UsageService usageService, AppOptions appOptions, ILogger<OverageMiddleware> logger) {
        _next = next;
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
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

        string organizationId = context.Request.GetDefaultOrganizationId();
        var organizationTask = _organizationRepository.GetByIdAsync(organizationId, o => o.Cache());

        string projectId = context.Request.GetDefaultProjectId();
        var projectTask = _projectRepository.GetByIdAsync(projectId, o => o.Cache());

        bool tooBig = false;
        if (String.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase) && context.Request.Headers != null) {
            if (context.Request.Headers.ContentLength.HasValue && context.Request.Headers.ContentLength.Value <= 0) {
                //_metricsClient.Counter(MetricNames.PostsBlocked);
                context.Response.StatusCode = StatusCodes.Status411LengthRequired;
                await Task.WhenAll(organizationTask, projectTask);
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

        var organization = await organizationTask;
        var project = await projectTask;

        // block large submissions, client should break them up or remove some of the data.
        if (tooBig) {
            await _usageService.IncrementTooBigAsync(organization, project).AnyContext();
            context.Response.StatusCode = StatusCodes.Status413RequestEntityTooLarge;
            return;
        }

        bool overLimit = await _usageService.IsOverLimitAsync(organization).AnyContext();
        if (overLimit) {
            AppDiagnostics.PostsBlocked.Add(1);
            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            return;
        }

        context.Request.SetOrganization(organization);
        context.Request.SetProject(project);
        await _next(context);
    }
}
