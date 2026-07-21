using System.Text;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Base;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Services;
using Exceptionless.Core.Validation;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Caching;
using Foundatio.Mediator;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Models;
using Foundatio.Serializer;
using Microsoft.Net.Http.Headers;

namespace Exceptionless.Web.Api.Handlers;

public class EventHandler(
    IEventRepository eventRepository,
    IOrganizationRepository organizationRepository,
    IProjectRepository projectRepository,
    IStackRepository stackRepository,
    EventPostService eventPostService,
    IQueue<EventUserDescription> eventUserDescriptionQueue,
    MiniValidationValidator miniValidationValidator,
    FormattingPluginManager formattingPluginManager,
    ICacheClient cacheClient,
    ITextSerializer serializer,
    PersistentEventQueryValidator validator,
    AppOptions appOptions,
    UsageService usageService,
    TimeProvider timeProvider,
    LinkGenerator linkGenerator,
    ILoggerFactory loggerFactory)
{
    private static readonly HashSet<string> _ignoredKeys = new(StringComparer.OrdinalIgnoreCase) { "access_token", "api_key", "apikey" };
    private readonly ILogger _logger = loggerFactory.CreateLogger<EventHandler>();
    private static readonly ICollection<string> _allowedDateFields = new List<string> { EventIndex.Alias.Date };
    private const string DefaultDateField = EventIndex.Alias.Date;
    private static Result<T> PlanLimitResult<T>(string message) => Result.Invalid(ValidationError.Create(ApiValidationErrorIdentifiers.PlanLimit, message));
    private static bool ShouldIncludeTotal(string? include) => ShouldInclude(include, "total");

    private static bool ShouldInclude(string? include, string value)
    {
        if (String.IsNullOrWhiteSpace(include))
            return false;

        return include
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Result<CountResult>> Handle(GetEventCount message)
    {
        var httpContext = message.Context;
        var organizations = await GetSelectedOrganizationsAsync(httpContext, message.Filter);
        if (organizations.All(o => o.IsSuspended))
            return CountResult.Empty;

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organizations.GetRetentionUtcCutoff(appOptions.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(organizations) { IsUserOrganizationsFilter = true };
        return await CountInternalAsync(sf, ti, httpContext, message.Filter, message.Aggregations, message.Mode);
    }

    public async Task<Result<CountResult>> Handle(GetEventCountByOrganization message)
    {
        var httpContext = message.Context;
        var organization = await GetOrganizationAsync(message.OrganizationId, httpContext);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (organization.IsSuspended)
            return PlanLimitResult<CountResult>("Unable to view event occurrences for the suspended organization.");

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(appOptions.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(organization);
        return await CountInternalAsync(sf, ti, httpContext, message.Filter, message.Aggregations, message.Mode);
    }

    public async Task<Result<CountResult>> Handle(GetEventCountByProject message)
    {
        var httpContext = message.Context;
        var project = await GetProjectAsync(message.ProjectId, httpContext);
        if (project is null)
            return Result.NotFound("Project not found.");

        var organization = await GetOrganizationAsync(project.OrganizationId, httpContext);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (organization.IsSuspended)
            return PlanLimitResult<CountResult>("Unable to view event occurrences for the suspended organization.");

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(project, appOptions.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(project, organization);
        return await CountInternalAsync(sf, ti, httpContext, message.Filter, message.Aggregations, message.Mode);
    }

    public async Task<Result<PersistentEvent>> Handle(GetEventById message)
    {
        var httpContext = message.Context;
        var model = await GetModelAsync(message.Id, httpContext, false);
        if (model is null)
            return Result.NotFound("Event not found.");

        if (!String.IsNullOrEmpty(message.ExpectedStackId) && !String.Equals(model.StackId, message.ExpectedStackId, StringComparison.Ordinal))
            return Result.BadRequest($"The event \"{model.Id}\" belongs to stack \"{model.StackId}\", not stack \"{message.ExpectedStackId}\". Open the event from its current stack.");

        var organization = await GetOrganizationAsync(model.OrganizationId, httpContext);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (organization.IsSuspended || organization.RetentionDays > 0 && model.Date.UtcDateTime < timeProvider.GetUtcNow().UtcDateTime.SubtractDays(organization.RetentionDays))
            return PlanLimitResult<PersistentEvent>("Unable to view event occurrence due to plan limits.");

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(appOptions.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(organization);
        var result = await eventRepository.GetPreviousAndNextEventIdsAsync(model, sf, ti.Range.UtcStart, ti.Range.UtcEnd);

        var links = new List<string>();
        AddLink("GetPersistentEventById", result.Previous, "previous");
        AddLink("GetPersistentEventById", result.Next, "next");
        AddLink("GetStackById", model.StackId, "parent");

        if (links.Count > 0)
            httpContext.Response.Headers[HeaderNames.Link] = links.ToArray();

        return model;

        void AddLink(string endpointName, string? id, string relationship)
        {
            if (String.IsNullOrEmpty(id))
                return;

            string? uri = linkGenerator.GetUriByName(httpContext, endpointName, new { id });
            if (!String.IsNullOrEmpty(uri))
                links.Add($"<{uri}>; rel=\"{relationship}\"");
        }
    }

    public async Task<Result<PagedResult<object>>> Handle(GetAllEvents message)
    {
        var httpContext = message.Context;
        var organizations = await GetSelectedOrganizationsAsync(httpContext, message.Filter);
        if (organizations.All(o => o.IsSuspended))
            return new PagedResult<object>(Array.Empty<PersistentEvent>(), false);

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organizations.GetRetentionUtcCutoff(appOptions.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(organizations) { IsUserOrganizationsFilter = true };
        return await GetInternalAsync(sf, ti, httpContext, message.Filter, message.Sort, message.Mode, message.Page, message.Limit, message.Before, message.After, includeTotal: ShouldIncludeTotal(message.Include));
    }

    public async Task<Result<PagedResult<object>>> Handle(GetEventsByOrganization message)
    {
        var httpContext = message.Context;
        var organization = await GetOrganizationAsync(message.OrganizationId, httpContext);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (organization.IsSuspended)
            return PlanLimitResult<PagedResult<object>>("Unable to view event occurrences for the suspended organization.");

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(appOptions.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(organization);
        return await GetInternalAsync(sf, ti, httpContext, message.Filter, message.Sort, message.Mode, message.Page, message.Limit, message.Before, message.After, includeTotal: ShouldIncludeTotal(message.Include));
    }

    public async Task<Result<PagedResult<object>>> Handle(GetEventsByProject message)
    {
        var httpContext = message.Context;
        var project = await GetProjectAsync(message.ProjectId, httpContext);
        if (project is null)
            return Result.NotFound("Project not found.");

        var organization = await GetOrganizationAsync(project.OrganizationId, httpContext);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (organization.IsSuspended)
            return PlanLimitResult<PagedResult<object>>("Unable to view event occurrences for the suspended organization.");

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(project, appOptions.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(project, organization);
        return await GetInternalAsync(sf, ti, httpContext, message.Filter, message.Sort, message.Mode, message.Page, message.Limit, message.Before, message.After, includeTotal: ShouldIncludeTotal(message.Include));
    }

    public async Task<Result<PagedResult<object>>> Handle(GetEventsByStack message)
    {
        var httpContext = message.Context;
        var stack = await GetStackAsync(message.StackId, httpContext);
        if (stack is null)
            return Result.NotFound("Stack not found.");

        var organization = await GetOrganizationAsync(stack.OrganizationId, httpContext);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (organization.IsSuspended)
            return PlanLimitResult<PagedResult<object>>("Unable to view event occurrences for the suspended organization.");

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(stack, appOptions.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(stack, organization);
        return await GetInternalAsync(sf, ti, httpContext, message.Filter, message.Sort, message.Mode, message.Page, message.Limit, message.Before, message.After, includeTotal: ShouldIncludeTotal(message.Include));
    }

    public async Task<Result<PagedResult<object>>> Handle(GetEventsByReferenceId message)
    {
        var httpContext = message.Context;
        var organizations = await GetSelectedOrganizationsAsync(httpContext);
        if (organizations.All(o => o.IsSuspended))
            return new PagedResult<object>(Array.Empty<PersistentEvent>(), false);

        var ti = TimeRangeParser.GetTimeInfo(null, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organizations.GetRetentionUtcCutoff(appOptions.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(organizations) { IsUserOrganizationsFilter = true };
        return await GetInternalAsync(sf, ti, httpContext, $"(reference:{message.ReferenceId} OR ref.{Event.KnownReferenceNames.Parent}:{message.ReferenceId})", null, message.Mode, message.Page, message.Limit, message.Before, message.After, includeTotal: ShouldIncludeTotal(message.Include));
    }

    public async Task<Result<PagedResult<object>>> Handle(GetEventsByReferenceIdAndProject message)
    {
        var httpContext = message.Context;
        var project = await GetProjectAsync(message.ProjectId, httpContext);
        if (project is null)
            return Result.NotFound("Project not found.");

        var organization = await GetOrganizationAsync(project.OrganizationId, httpContext);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (organization.IsSuspended)
            return PlanLimitResult<PagedResult<object>>("Unable to view event occurrences for the suspended organization.");

        var ti = TimeRangeParser.GetTimeInfo(null, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(project, appOptions.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(project, organization);
        return await GetInternalAsync(sf, ti, httpContext, $"(reference:{message.ReferenceId} OR ref.{Event.KnownReferenceNames.Parent}:{message.ReferenceId})", null, message.Mode, message.Page, message.Limit, message.Before, message.After, includeTotal: ShouldIncludeTotal(message.Include));
    }

    public async Task<Result<PagedResult<object>>> Handle(GetEventsBySessionId message)
    {
        var httpContext = message.Context;
        var organizations = await GetSelectedOrganizationsAsync(httpContext, message.Filter);
        if (organizations.All(o => o.IsSuspended))
            return new PagedResult<object>(Array.Empty<PersistentEvent>(), false);

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organizations.GetRetentionUtcCutoff(appOptions.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(organizations) { IsUserOrganizationsFilter = true };
        return await GetInternalAsync(sf, ti, httpContext, $"(reference:{message.SessionId} OR ref.session:{message.SessionId}) {message.Filter}", message.Sort, message.Mode, message.Page, message.Limit, message.Before, message.After, usesPremiumFeatures: true, includeTotal: ShouldIncludeTotal(message.Include));
    }

    public async Task<Result<PagedResult<object>>> Handle(GetEventsBySessionIdAndProject message)
    {
        var httpContext = message.Context;
        var project = await GetProjectAsync(message.ProjectId, httpContext);
        if (project is null)
            return Result.NotFound("Project not found.");

        var organization = await GetOrganizationAsync(project.OrganizationId, httpContext);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (organization.IsSuspended)
            return PlanLimitResult<PagedResult<object>>("Unable to view event occurrences for the suspended organization.");

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(project, appOptions.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(project, organization);
        return await GetInternalAsync(sf, ti, httpContext, $"(reference:{message.SessionId} OR ref.session:{message.SessionId}) {message.Filter}", message.Sort, message.Mode, message.Page, message.Limit, message.Before, message.After, usesPremiumFeatures: true, includeTotal: ShouldIncludeTotal(message.Include));
    }

    public async Task<Result<PagedResult<object>>> Handle(GetSessions message)
    {
        var httpContext = message.Context;
        var organizations = await GetSelectedOrganizationsAsync(httpContext, message.Filter);
        if (organizations.All(o => o.IsSuspended))
            return new PagedResult<object>(Array.Empty<PersistentEvent>(), false);

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organizations.GetRetentionUtcCutoff(appOptions.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(organizations) { IsUserOrganizationsFilter = true };
        return await GetInternalAsync(sf, ti, httpContext, $"type:{Event.KnownTypes.Session} {message.Filter}", message.Sort, message.Mode, message.Page, message.Limit, message.Before, message.After, usesPremiumFeatures: true, includeTotal: ShouldIncludeTotal(message.Include));
    }

    public async Task<Result<PagedResult<object>>> Handle(GetSessionsByOrganization message)
    {
        var httpContext = message.Context;
        var organization = await GetOrganizationAsync(message.OrganizationId, httpContext);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (organization.IsSuspended)
            return PlanLimitResult<PagedResult<object>>("Unable to view event occurrences for the suspended organization.");

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(appOptions.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(organization);
        return await GetInternalAsync(sf, ti, httpContext, $"type:{Event.KnownTypes.Session} {message.Filter}", message.Sort, message.Mode, message.Page, message.Limit, message.Before, message.After, usesPremiumFeatures: true, includeTotal: ShouldIncludeTotal(message.Include));
    }

    public async Task<Result<PagedResult<object>>> Handle(GetSessionsByProject message)
    {
        var httpContext = message.Context;
        var project = await GetProjectAsync(message.ProjectId, httpContext);
        if (project is null)
            return Result.NotFound("Project not found.");

        var organization = await GetOrganizationAsync(project.OrganizationId, httpContext);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (organization.IsSuspended)
            return PlanLimitResult<PagedResult<object>>("Unable to view event occurrences for the suspended organization.");

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(project, appOptions.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(project, organization);
        return await GetInternalAsync(sf, ti, httpContext, $"type:{Event.KnownTypes.Session} {message.Filter}", message.Sort, message.Mode, message.Page, message.Limit, message.Before, message.After, usesPremiumFeatures: true, includeTotal: ShouldIncludeTotal(message.Include));
    }

    public async Task<Result> Handle(SetEventUserDescription message)
    {
        var httpContext = message.Context;
        string? claimProjectId = httpContext.Request.GetProjectId();
        if (message.ProjectId is not null && claimProjectId is not null && !String.Equals(message.ProjectId, claimProjectId))
        {
            _logger.ProjectRouteDoesNotMatch(claimProjectId, message.ProjectId);
            return Result.NotFound("Project not found.");
        }

        if (String.IsNullOrEmpty(message.ReferenceId))
            return Result.NotFound("Event not found.");

        string? projectId = message.ProjectId ?? claimProjectId ?? httpContext.Request.GetDefaultProjectId();

        if (String.IsNullOrEmpty(projectId))
            return Result.BadRequest("No project id specified and no default project was found");

        var (isValid, errors) = await miniValidationValidator.ValidateAsync(message.Description);
        if (!isValid)
        {
            return Result.Invalid(errors.SelectMany(e => e.Value.Select(validationMessage => ValidationError.Create(e.Key, validationMessage))));
        }

        var project = await GetProjectAsync(projectId, httpContext);
        if (project is null)
            return Result.NotFound("Project not found.");

        // Set the project for the configuration response filter.
        httpContext.Request.SetProject(project);

        var eventUserDescription = new EventUserDescription
        {
            ProjectId = project.Id,
            ReferenceId = message.ReferenceId,
            EmailAddress = message.Description.EmailAddress,
            Description = message.Description.Description,
            Data = message.Description.Data
        };

        await eventUserDescriptionQueue.EnqueueAsync(eventUserDescription);
        return Result.Accepted();
    }

    public async Task<Result> Handle(LegacyPatchEvent message)
    {
        var httpContext = message.Context;
        if (message.Changes is null)
            return Result.Success();

        var changes = message.Changes;
        if (changes.UnknownProperties.TryGetValue("UserEmail", out object? value))
            changes.TrySetPropertyValue("EmailAddress", value);
        if (changes.UnknownProperties.TryGetValue("UserDescription", out value))
            changes.TrySetPropertyValue("Description", value);

        var userDescription = new UserDescription();
        changes.Patch(userDescription);

        return await Handle(new SetEventUserDescription(message.Id, userDescription, null, httpContext));
    }

    public async Task<Result> Handle(RecordEventHeartbeat message)
    {
        var httpContext = message.Context;
        if (appOptions.EventSubmissionDisabled || String.IsNullOrEmpty(message.Id))
            return Result.Success();

        string? projectId = httpContext.Request.GetDefaultProjectId();
        if (String.IsNullOrEmpty(projectId))
            return Result.BadRequest("No project id specified and no default project was found.");

        string identityHash = message.Id.ToSHA1();
        string heartbeatCacheKey = String.Concat("Project:", projectId, ":heartbeat:", identityHash);
        try
        {
            await Task.WhenAll(
                cacheClient.SetAsync(heartbeatCacheKey, timeProvider.GetUtcNow().UtcDateTime, TimeSpan.FromHours(2)),
                message.Close ? cacheClient.SetAsync(String.Concat(heartbeatCacheKey, "-close"), true, TimeSpan.FromHours(2)) : Task.CompletedTask
            );
        }
        catch (Exception ex)
        {
            if (projectId != appOptions.InternalProjectId)
            {
                using var _ = _logger.BeginScope(new ExceptionlessState().Project(projectId).Property("Id", message.Id).Property("Close", message.Close).SetHttpContext(httpContext));
                _logger.LogError(ex, "Error enqueuing session heartbeat: {Message}", ex.Message);
            }

            throw;
        }

        return Result.Success();
    }

    public async Task<Result> Handle(SubmitEventByGet message)
    {
        var httpContext = message.Context;
        string? claimProjectId = httpContext.Request.GetProjectId();
        if (message.ProjectId is not null && claimProjectId is not null && !String.Equals(message.ProjectId, claimProjectId))
        {
            _logger.ProjectRouteDoesNotMatch(claimProjectId, message.ProjectId);
            return Result.NotFound("Project not found.");
        }

        var filteredParameters = httpContext.Request.Query.Where(p => !String.IsNullOrEmpty(p.Key) && !p.Value.All(String.IsNullOrEmpty) && !_ignoredKeys.Contains(p.Key)).ToList();
        if (filteredParameters.Count == 0)
            return Result.Success();

        string? projectId = message.ProjectId ?? claimProjectId ?? httpContext.Request.GetDefaultProjectId();

        if (String.IsNullOrEmpty(projectId))
            return Result.BadRequest("No project id specified and no default project was found");

        var project = await GetProjectAsync(projectId, httpContext);
        if (project is null)
            return Result.NotFound("Project not found.");

        // Set the project for the configuration response filter.
        httpContext.Request.SetProject(project);

        string? contentEncoding = httpContext.Request.Headers.TryGetAndReturn(Headers.ContentEncoding);
        var ev = new Event
        {
            Type = !String.IsNullOrEmpty(message.Type) ? message.Type : Event.KnownTypes.Log
        };

        string? identity = null;
        string? identityName = null;

        var exclusions = project.Configuration.Settings.GetStringCollection(SettingsDictionary.KnownKeys.DataExclusions).ToList();
        foreach (var kvp in filteredParameters)
        {
            switch (kvp.Key.ToLowerInvariant())
            {
                case "type":
                    ev.Type = kvp.Value.FirstOrDefault();
                    break;
                case "source":
                    ev.Source = kvp.Value.FirstOrDefault();
                    break;
                case "message":
                    ev.Message = kvp.Value.FirstOrDefault();
                    break;
                case "reference":
                    ev.ReferenceId = kvp.Value.FirstOrDefault();
                    break;
                case "date":
                    if (DateTimeOffset.TryParse(kvp.Value.FirstOrDefault(), out var dtValue))
                        ev.Date = dtValue;
                    break;
                case "count":
                    if (Int32.TryParse(kvp.Value.FirstOrDefault(), out int intValue))
                        ev.Count = intValue;
                    break;
                case "value":
                    if (Decimal.TryParse(kvp.Value.FirstOrDefault(), out decimal decValue))
                        ev.Value = decValue;
                    break;
                case "geo":
                    if (GeoResult.TryParse(kvp.Value.FirstOrDefault(), out var geo))
                        ev.Geo = geo?.ToString();
                    break;
                case "tags":
                    ev.Tags ??= [];
                    ev.Tags.AddRange(kvp.Value.SelectMany(t => t?.Split([","], StringSplitOptions.RemoveEmptyEntries) ?? []).Distinct());
                    break;
                case "identity":
                    identity = kvp.Value.FirstOrDefault();
                    break;
                case "identity.name":
                    identityName = kvp.Value.FirstOrDefault();
                    break;
                default:
                    if (kvp.Key.AnyWildcardMatches(exclusions, true))
                        continue;

                    ev.Data![kvp.Key] = kvp.Value.Count > 1 ? kvp.Value : kvp.Value.FirstOrDefault();

                    break;
            }
        }

        if (identity != null)
            ev.SetUserIdentity(identity, identityName);

        try
        {
            string mediaType = String.Empty;
            string charSet = String.Empty;
            if (httpContext.Request.ContentType is not null && MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var contentTypeHeader))
            {
                mediaType = contentTypeHeader.MediaType.ToString();
                charSet = contentTypeHeader.Charset.ToString();
            }

            using var stream = new MemoryStream(ev.GetBytes(serializer));
            await eventPostService.EnqueueAsync(new EventPost(appOptions.EnableArchive)
            {
                ApiVersion = message.ApiVersion,
                CharSet = charSet,
                ContentEncoding = contentEncoding,
                IpAddress = httpContext.Request.GetClientIpAddress(),
                MediaType = mediaType,
                OrganizationId = project.OrganizationId,
                ProjectId = project.Id,
                UserAgent = message.UserAgent
            }, stream);
        }
        catch (Exception ex)
        {
            if (projectId != appOptions.InternalProjectId)
            {
                using var _ = _logger.BeginScope(new ExceptionlessState().Project(projectId).SetHttpContext(httpContext));
                _logger.LogError(ex, "Error enqueuing event post: {Message}", ex.Message);
            }

            throw;
        }

        return Result.Success();
    }

    public async Task<Result> Handle(SubmitEventByPost message)
    {
        var httpContext = message.Context;
        string? claimProjectId = httpContext.Request.GetProjectId();
        if (message.ProjectId is not null && claimProjectId is not null && !String.Equals(message.ProjectId, claimProjectId))
        {
            _logger.ProjectRouteDoesNotMatch(claimProjectId, message.ProjectId);
            return Result.NotFound("Project not found.");
        }

        if (httpContext.Request.ContentLength is <= 0)
            return Result.Accepted();

        string? projectId = message.ProjectId ?? claimProjectId ?? httpContext.Request.GetDefaultProjectId();

        if (String.IsNullOrEmpty(projectId))
            return Result.BadRequest("No project id specified and no default project was found");

        var project = await GetProjectAsync(projectId, httpContext);
        if (project is null)
            return Result.NotFound("Project not found.");

        // Set the project for the configuration response filter.
        httpContext.Request.SetProject(project);

        try
        {
            string mediaType = String.Empty;
            string charSet = String.Empty;
            if (httpContext.Request.ContentType is not null)
            {
                var contentType = MediaTypeHeaderValue.Parse(httpContext.Request.ContentType);
                mediaType = contentType.MediaType.ToString();
                charSet = contentType.Charset.ToString();
            }

            Stream requestBody = appOptions.MaximumEventPostSize > 0
                ? new EventPostRequestBodyStream(httpContext.Request.Body, appOptions.MaximumEventPostSize)
                : httpContext.Request.Body;

            var result = await eventPostService.SaveAndEnqueueAsync(new EventPost(appOptions.EnableArchive)
            {
                ApiVersion = message.ApiVersion,
                CharSet = charSet,
                ContentEncoding = httpContext.Request.Headers.TryGetAndReturn(Headers.ContentEncoding),
                IpAddress = httpContext.Request.GetClientIpAddress(),
                MediaType = mediaType,
                OrganizationId = project.OrganizationId,
                ProjectId = project.Id,
                UserAgent = message.UserAgent,
            }, requestBody, httpContext.RequestAborted);

            if (result.IsRejected)
            {
                if (result.RejectedStatusCode == StatusCodes.Status413RequestEntityTooLarge)
                    await usageService.IncrementTooBigAsync(project.OrganizationId, project.Id);

                if (result.RejectedStatusCode == StatusCodes.Status413RequestEntityTooLarge)
                    return Result.Invalid(ValidationError.Create(ApiValidationErrorIdentifiers.RequestEntityTooLarge, result.RejectionReason ?? "Request body too large."));

                return Result.BadRequest(result.RejectionReason ?? "Request body was rejected.");
            }
        }
        catch (Exception ex)
        {
            if (projectId != appOptions.InternalProjectId)
            {
                using var _ = _logger.BeginScope(new ExceptionlessState().Project(projectId).SetHttpContext(httpContext));
                _logger.LogError(ex, "Error enqueuing event post: {Message}", ex.Message);
            }

            throw;
        }

        return Result.Accepted();
    }

    public async Task<Result<WorkInProgressResult>> Handle(DeleteEvents message)
    {
        var httpContext = message.Context;
        var ids = message.Ids.FromDelimitedString();
        var items = await GetModelsAsync(ids, httpContext, false);
        if (items.Count == 0)
            return Result.NotFound("Events not found.");

        var results = new ModelActionResults();
        results.AddNotFound(ids.Except(items.Select(i => i.Id)));

        var denied = items.Where(model => !httpContext.Request.CanAccessOrganization(model.OrganizationId)).ToList();
        foreach (var model in denied)
            results.Failure.Add(PermissionResult.DenyWithNotFound(model.Id));

        var list = items.Where(model => httpContext.Request.CanAccessOrganization(model.OrganizationId)).ToList();

        if (list.Count == 0)
            return results.Failure.Count == 1 ? PermissionToResult(results.Failure.First()) : results;

        var currentUser = httpContext.Request.GetUser();
        var projectGroups = list.GroupBy(ev => new { ev.OrganizationId, ev.ProjectId }).ToList();
        foreach (var projectGroup in projectGroups)
        {
            var ev = projectGroup.First();
            using var _ = _logger.BeginScope(new ExceptionlessState().Organization(ev.OrganizationId).Project(ev.ProjectId).Tag("Delete").Identity(currentUser.EmailAddress).Property("User", currentUser).SetHttpContext(httpContext));
            _logger.LogInformation("User {User} deleted {RemovedCount} events in project ({ProjectId})", currentUser.Id, projectGroup.Count(), ev.ProjectId);
        }

        await eventRepository.RemoveAsync(list);

        foreach (var projectGroup in projectGroups)
        {
            try
            {
                await usageService.IncrementDeletedAsync(projectGroup.Key.OrganizationId, projectGroup.Key.ProjectId, projectGroup.Count());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to increment deleted usage metrics for org {OrganizationId} project {ProjectId}: {Message}", projectGroup.Key.OrganizationId, projectGroup.Key.ProjectId, ex.Message);
            }
        }

        if (results.Failure.Count == 0)
            return new WorkInProgressResult();

        results.Success.AddRange(list.Select(i => i.Id));
        return results;
    }

    #region Private Helpers

    private async Task<Result<CountResult>> CountInternalAsync(AppFilter sf, TimeInfo ti, HttpContext httpContext, string? filter = null, string? aggregations = null, string? mode = null)
    {
        var pr = await validator.ValidateQueryAsync(filter);
        if (!pr.IsValid)
            return Result.BadRequest(pr.Message ?? "Invalid filter.");

        var far = await validator.ValidateAggregationsAsync(aggregations);
        if (!far.IsValid)
            return Result.BadRequest(far.Message ?? "Invalid aggregations.");

        sf.UsesPremiumFeatures = pr.UsesPremiumFeatures || far.UsesPremiumFeatures;

        if (mode == "stack_new")
            filter = AddFirstOccurrenceFilter(ti.Range, filter);

        var query = new RepositoryQuery<PersistentEvent>()
            .AppFilter(ShouldApplySystemFilter(sf, filter, httpContext.Request) ? sf : null)
            .DateRange(ti.Range.UtcStart, ti.Range.UtcEnd, ti.Field)
            .Index(ti.Range.UtcStart, ti.Range.UtcEnd);

        CountResult result;
        try
        {
            result = await eventRepository.CountAsync(q => q.SystemFilter(query).FilterExpression(filter).EnforceEventStackFilter().AggregationsExpression(aggregations));
        }
        catch (Exception ex)
        {
            var currentUser = httpContext.Request.GetUser();
            using var _ = _logger.BeginScope(new ExceptionlessState().Property("Search Filter", new { SystemFilter = sf, UserFilter = filter, Time = ti, Aggregations = aggregations }).Tag("Search").Identity(currentUser.EmailAddress).Property("User", currentUser).SetHttpContext(httpContext));
            _logger.LogError(ex, "An error has occurred. Please check your filter or aggregations: {Message}", ex.Message);

            throw;
        }

        return result;
    }

    private async Task<Result<PagedResult<object>>> GetInternalAsync(AppFilter sf, TimeInfo ti, HttpContext httpContext, string? filter = null, string? sort = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null, bool usesPremiumFeatures = false, bool includeTotal = false)
    {
        var currentUser = httpContext.Request.GetUser();
        using var _ = _logger.BeginScope(new ExceptionlessState()
            .Property("Search Filter", new
            {
                Mode = mode,
                SystemFilter = sf,
                UserFilter = filter,
                Time = ti,
                Page = page,
                Limit = limit,
                Before = before,
                After = after
            })
            .Tag("Search")
            .Identity(currentUser.EmailAddress)
            .Property("User", currentUser)
            .SetHttpContext(httpContext)
        );

        int resolvedPage = Pagination.GetPage(page.GetValueOrDefault(1));
        limit = Pagination.GetLimit(limit);
        int skip = Pagination.GetSkip(resolvedPage, limit);
        if (skip > Pagination.MaximumSkip)
            return new PagedResult<object>(Array.Empty<PersistentEvent>(), false);

        var pr = await validator.ValidateQueryAsync(filter);
        if (!pr.IsValid)
            return Result.BadRequest(pr.Message ?? "Invalid filter.");

        sf.UsesPremiumFeatures = pr.UsesPremiumFeatures || usesPremiumFeatures;

        try
        {
            FindResults<PersistentEvent> events;
            switch (mode)
            {
                case "summary":
                    events = await GetEventsInternalAsync(sf, ti, filter, sort, page, limit, before, after, includeTotal, httpContext.Request);
                    var summaries = events.Documents.Select(e =>
                    {
                        var summaryData = formattingPluginManager.GetEventSummaryData(e);
                        return new EventSummaryModel
                        {
                            Id = summaryData.Id,
                            TemplateKey = summaryData.TemplateKey,
                            Date = e.Date,
                            Type = e.Type,
                            Version = e.GetVersion(),
                            Data = summaryData.Data
                        };
                    }).ToList();
                    return new PagedResult<object>(summaries.Cast<object>().ToList(), events.HasMore && !Pagination.NextPageExceedsSkipLimit(page, limit), page, includeTotal ? events.Total : null, events.Hits.FirstOrDefault()?.GetSortToken(serializer), events.Hits.LastOrDefault()?.GetSortToken(serializer));
                case "stack_recent":
                case "stack_frequent":
                case "stack_new":
                case "stack_users":
                    if (!String.IsNullOrEmpty(sort))
                        return Result.BadRequest("Sort is not supported in stack mode.");

                    var systemFilter = new RepositoryQuery<PersistentEvent>()
                        .AppFilter(ShouldApplySystemFilter(sf, filter, httpContext.Request) ? sf : null)
                        .EnforceEventStackFilter()
                        .DateRange(ti.Range.UtcStart, ti.Range.UtcEnd, (PersistentEvent e) => e.Date)
                        .Index(ti.Range.UtcStart, ti.Range.UtcEnd);

                    string? stackAggregations = mode switch
                    {
                        "stack_recent" => "cardinality:user sum:count~1 min:date -max:date",
                        "stack_frequent" => "cardinality:user -sum:count~1 min:date max:date",
                        "stack_new" => "cardinality:user sum:count~1 -min:date max:date",
                        "stack_users" => "-cardinality:user sum:count~1 min:date max:date",
                        _ => null
                    };

                    if (mode == "stack_new")
                        filter = AddFirstOccurrenceFilter(ti.Range, filter);

                    string aggregationExpression = includeTotal
                        ? $"cardinality:stack_id terms:(stack_id~{Pagination.GetSkip(resolvedPage + 1, limit) + 1} {stackAggregations})"
                        : $"terms:(stack_id~{Pagination.GetSkip(resolvedPage + 1, limit) + 1} {stackAggregations})";

                    var countResponse = await eventRepository.CountAsync(q => q
                        .SystemFilter(systemFilter)
                        .FilterExpression(filter)
                        .EnforceEventStackFilter()
                        .AggregationsExpression(aggregationExpression),
                        o => o.TrackTotalHits(false));

                    var stackTerms = countResponse.Aggregations.Terms<string>("terms_stack_id");
                    if (stackTerms is null || stackTerms.Buckets.Count == 0)
                        return new PagedResult<object>(Array.Empty<PersistentEvent>(), false);

                    string[] stackIds = stackTerms.Buckets.Skip(skip).Take(limit + 1).Select(t => t.Key).ToArray();
                    var stacks = (await stackRepository.GetByIdsAsync(stackIds)).Select(s => s.ApplyOffset(ti.Offset)).ToList();

                    var stackSummaries = await GetStackSummariesAsync(stacks, stackTerms.Buckets, sf, ti);

                    double? totalStackCount = countResponse.Aggregations.Cardinality("cardinality_stack_id")?.Value;
                    long? total = includeTotal && totalStackCount.HasValue ? Convert.ToInt64(totalStackCount.Value) : null;
                    return new PagedResult<object>(stackSummaries.Take(limit).Cast<object>().ToList(), stackSummaries.Count > limit && !Pagination.NextPageExceedsSkipLimit(resolvedPage, limit), resolvedPage, total);
                default:
                    events = await GetEventsInternalAsync(sf, ti, filter, sort, page, limit, before, after, includeTotal, httpContext.Request);
                    return new PagedResult<object>(events.Documents.Cast<object>().ToList(), events.HasMore && !Pagination.NextPageExceedsSkipLimit(page, limit), page, includeTotal ? events.Total : null, events.Hits.FirstOrDefault()?.GetSortToken(serializer), events.Hits.LastOrDefault()?.GetSortToken(serializer));
            }
        }
        catch (ApplicationException ex)
        {
            string message = "An error has occurred: Please check your search filter.";
            if (ex is DocumentLimitExceededException)
                message = $"An error has occurred: {ex.Message ?? "Please limit your search criteria."}";

            _logger.LogError(ex, message);
            throw;
        }
    }

    private static string AddFirstOccurrenceFilter(DateTimeRange timeRange, string? filter)
    {
        bool inverted = false;
        if (filter is not null && filter.StartsWith("@!"))
        {
            inverted = true;
            filter = filter.Substring(2);
        }

        var sb = new StringBuilder();
        if (inverted)
            sb.Append("@!");

        sb.Append("first_occurrence:[\"");
        sb.Append(timeRange.UtcStart.ToString("O"));
        sb.Append("\" TO \"");
        sb.Append(timeRange.UtcEnd.ToString("O"));
        sb.Append("\"]");

        if (String.IsNullOrEmpty(filter))
            return sb.ToString();

        sb.Append(' ');

        bool isGrouped = filter.StartsWith('(') && filter.EndsWith(')');

        if (isGrouped)
            sb.Append(filter);
        else
            sb.Append('(').Append(filter).Append(')');

        return sb.ToString();
    }

    private Task<FindResults<PersistentEvent>> GetEventsInternalAsync(AppFilter sf, TimeInfo ti, string? filter, string? sort, int? page, int limit, string? before, string? after, bool includeTotal, HttpRequest? request = null)
    {
        if (String.IsNullOrEmpty(sort))
            sort = $"-{EventIndex.Alias.Date}";

        return eventRepository.FindAsync(
            q => q.AppFilter(ShouldApplySystemFilter(sf, filter, request) ? sf : null)
                .FilterExpression(filter)
                .EnforceEventStackFilter()
                .SortExpression(sort)
                .DateRange(ti.Range.UtcStart, ti.Range.UtcEnd, ti.Field)
                .Index(ti.Range.UtcStart, ti.Range.UtcEnd),
            o => page.HasValue
                ? o.PageNumber(page).PageLimit(limit).TrackTotalHits(includeTotal)
                : o.SearchBeforeToken(before, serializer).SearchAfterToken(after, serializer).PageLimit(limit).TrackTotalHits(includeTotal));
    }

    private static bool ShouldApplySystemFilter(AppFilter sf, string? filter, HttpRequest? request = null)
    {
        // Apply filter to non admin users.
        if (request is null || !request.IsGlobalAdmin())
            return true;

        // Apply filter as it's scoped via a controller action.
        if (!sf.IsUserOrganizationsFilter)
            return true;

        // Empty user filter
        if (String.IsNullOrEmpty(filter))
            return true;

        // Used for impersonating a user. Only skip the filter if it contains an org, project or stack.
        var scope = GetFilterScopeVisitor.Run(filter);
        return !scope.HasScope;
    }

    private async Task<ICollection<StackSummaryModel>> GetStackSummariesAsync(List<Stack> stacks, IReadOnlyCollection<KeyedBucket<string>> stackTerms, AppFilter sf, TimeInfo ti)
    {
        if (stacks.Count == 0)
            return new List<StackSummaryModel>(0);

        var totalUsers = await GetUserCountByProjectIdsAsync(stacks, sf, ti.Range.UtcStart, ti.Range.UtcEnd);
        return stacks.Join(stackTerms, s => s.Id, tk => tk.Key, (stack, term) =>
        {
            var data = formattingPluginManager.GetStackSummaryData(stack);
            var summary = new StackSummaryModel
            {
                Id = data.Id,
                TemplateKey = data.TemplateKey,
                Data = data.Data,
                Title = stack.Title,
                Status = stack.Status,
                FirstOccurrence = term.Aggregations.Min<DateTime>("min_date")?.Value ?? stack.FirstOccurrence,
                LastOccurrence = term.Aggregations.Max<DateTime>("max_date")?.Value ?? stack.LastOccurrence,
                Total = (long)(term.Aggregations.Sum("sum_count")?.Value ?? term.Total.GetValueOrDefault()),

                Users = term.Aggregations.Cardinality("cardinality_user")?.Value.GetValueOrDefault() ?? 0,
                TotalUsers = totalUsers.GetOrDefault(stack.ProjectId)
            };

            return summary;
        }).ToList();
    }

    private async Task<Dictionary<string, double>> GetUserCountByProjectIdsAsync(ICollection<Stack> stacks, AppFilter sf, DateTime utcStart, DateTime utcEnd)
    {
        using var scopedCacheClient = new ScopedCacheClient(cacheClient, $"Project:user-count:{utcStart.Floor(TimeSpan.FromMinutes(15)).Ticks}-{utcEnd.Floor(TimeSpan.FromMinutes(15)).Ticks}");
        var projectIds = stacks.Select(s => s.ProjectId).Distinct().ToList();
        var cachedTotals = await scopedCacheClient.GetAllAsync<double>(projectIds);

        var totals = cachedTotals.Where(kvp => kvp.Value.HasValue).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
        if (totals.Count == projectIds.Count)
            return totals;

        var systemFilter = new RepositoryQuery<PersistentEvent>().AppFilter(sf).DateRange(utcStart, utcEnd, (PersistentEvent e) => e.Date).Index(utcStart, utcEnd);
        var projects = cachedTotals
            .Where(kvp => !kvp.Value.HasValue && stacks.Contains(s => s.ProjectId == kvp.Key))
            .Select(kvp => new Project { Id = kvp.Key, OrganizationId = stacks.First(s => s.ProjectId == kvp.Key).OrganizationId })
            .ToList();
        var countResult = await eventRepository.CountAsync(q => q.SystemFilter(systemFilter).FilterExpression(projects.BuildFilter()).EnforceEventStackFilter().AggregationsExpression("terms:(project_id cardinality:user)"));

        var projectTerms = countResult.Aggregations.Terms<string>("terms_project_id")?.Buckets ?? [];
        var aggregations = projectTerms.ToDictionary(t => t.Key, t => t.Aggregations.Cardinality("cardinality_user")?.Value.GetValueOrDefault() ?? 0);
        await scopedCacheClient.SetAllAsync(aggregations.Where(t => t.Value >= 10).ToDictionary(k => k.Key, v => v.Value), TimeSpan.FromMinutes(5));
        totals.AddRange(aggregations);

        return totals;
    }

    private async Task<PersistentEvent?> GetModelAsync(string id, HttpContext httpContext, bool useCache = true)
    {
        if (String.IsNullOrEmpty(id))
            return null;

        var model = await eventRepository.GetByIdAsync(id, o => o.Cache(useCache));
        if (model is null)
            return null;

        if (!httpContext.Request.CanAccessOrganization(model.OrganizationId))
            return null;

        return model;
    }

    private async Task<IList<PersistentEvent>> GetModelsAsync(string[] ids, HttpContext httpContext, bool useCache = true)
    {
        if (ids.Length == 0)
            return [];

        var models = await eventRepository.GetByIdsAsync(ids, o => o.Cache(useCache));
        return models.Where(m => httpContext.Request.CanAccessOrganization(m.OrganizationId)).ToList();
    }

    private Task<Organization?> GetOrganizationAsync(string organizationId, HttpContext httpContext, bool useCache = true)
    {
        if (String.IsNullOrEmpty(organizationId) || !httpContext.Request.CanAccessOrganization(organizationId))
            return Task.FromResult<Organization?>(null);

        return organizationRepository.GetByIdAsync(organizationId, o => o.Cache(useCache));
    }

    private async Task<Project?> GetProjectAsync(string? projectId, HttpContext httpContext, bool useCache = true)
    {
        if (String.IsNullOrEmpty(projectId))
            return null;

        var project = await projectRepository.GetByIdAsync(projectId, o => o.Cache(useCache));
        if (project is null || !httpContext.Request.CanAccessOrganization(project.OrganizationId))
            return null;

        return project;
    }

    private async Task<Stack?> GetStackAsync(string stackId, HttpContext httpContext, bool useCache = true)
    {
        if (String.IsNullOrEmpty(stackId))
            return null;

        var stack = await stackRepository.GetByIdAsync(stackId, o => o.Cache(useCache));
        if (stack is null || !httpContext.Request.CanAccessOrganization(stack.OrganizationId))
            return null;

        return stack;
    }

    private async Task<IReadOnlyCollection<Organization>> GetSelectedOrganizationsAsync(HttpContext httpContext, string? filter = null)
    {
        var associatedOrganizationIds = httpContext.Request.GetAssociatedOrganizationIds();
        if (associatedOrganizationIds.Count == 0)
            return Array.Empty<Organization>();

        if (!String.IsNullOrEmpty(filter))
        {
            var scope = GetFilterScopeVisitor.Run(filter);
            if (scope.IsScopable)
            {
                Organization? organization = null;
                if (scope.OrganizationId is not null)
                {
                    organization = await organizationRepository.GetByIdAsync(scope.OrganizationId, o => o.Cache());
                }
                else if (scope.ProjectId is not null)
                {
                    var project = await projectRepository.GetByIdAsync(scope.ProjectId, o => o.Cache());
                    if (project is not null)
                        organization = await organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
                }
                else if (scope.StackId is not null)
                {
                    var stack = await stackRepository.GetByIdAsync(scope.StackId, o => o.Cache());
                    if (stack is not null)
                        organization = await organizationRepository.GetByIdAsync(stack.OrganizationId, o => o.Cache());
                }

                if (organization is not null)
                {
                    if (associatedOrganizationIds.Contains(organization.Id) || httpContext.Request.IsGlobalAdmin())
                        return new[] { organization };

                    return Array.Empty<Organization>();
                }
            }
        }

        return await organizationRepository.GetByIdsAsync(associatedOrganizationIds.ToArray(), o => o.Cache());
    }

    private static Result<WorkInProgressResult> PermissionToResult(PermissionResult permission)
    {
        if (!String.IsNullOrEmpty(permission.Message))
            return Result.NotFound(permission.Message);

        return Result.NotFound("Access denied.");
    }

    #endregion
}
