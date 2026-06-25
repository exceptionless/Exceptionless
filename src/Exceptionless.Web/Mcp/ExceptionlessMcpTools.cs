using System.ComponentModel;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Utility;
using Exceptionless.Web.Extensions;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Models;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Exceptionless.Web.Mcp;

[McpServerToolType]
public sealed class ExceptionlessMcpTools
{
    private const int DefaultLimit = 10;
    private const int MaxSummaryLimit = 100;
    private const int DefaultGroupLimit = 10;
    private const int MaxGroupLimit = 25;
    private const int DefaultMaxDetailSize = 16 * 1024;
    private const int MaxDetailSize = 64 * 1024;
    private const int MinDetailSize = 1024;
    private const string SummaryLimitDescription = "Maximum number of summary rows to return. Defaults to 10 and is capped at 100. Use the returned pagination.after or pagination.before cursor with the same limit to page through additional results.";
    private const string LastDescription = "Optional relative time range such as 24h, 7d, or 30m. Do not combine with startUtc or endUtc.";
    private const string StartUtcDescription = "Optional inclusive UTC start time, for example 2026-06-25T00:00:00Z. Do not combine with last.";
    private const string EndUtcDescription = "Optional exclusive UTC end time, for example 2026-06-25T01:00:00Z. Do not combine with last.";
    private const string EventGroupByDescription = "Optional dimension to group counts by. Supported values: version, type, source, status, tag, stack, user, level, error.type, error.code, os, os.version, browser.";
    private const string SnoozeDurationDescription = "Optional relative snooze duration such as 2h, 3d, or 1w. Do not combine with snoozeUntilUtc.";
    private const string ProjectFilterDescription = "Optional Exceptionless filter expression applied to projects. Supported fields: id, name, organization_id, created_utc, updated_utc, last_event_date_utc.";
    private const string StackFilterDescription = "Optional Exceptionless filter expression. Supported fields include: stack, project, project_id, organization, organization_id, type, status, title, description, tag, tags, references, fixed, hidden, regressed, error, first, first_occurrence, last, last_occurrence, occurrences, total_occurrences, data.*, idx.*.";
    private const string EventFilterDescription = "Optional Exceptionless filter expression applied to events. Supported fields include: id, project, project_id, stack, stack_id, organization, organization_id, type, source, message, date, tag, tags, user, user.name, user.email, path, error, error.type, error.message, error.code, status, data.*, idx.*.";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly StackQueryValidator _stackQueryValidator;
    private readonly PersistentEventQueryValidator _eventQueryValidator;
    private readonly SemanticVersionParser _semanticVersionParser;
    private readonly ITextSerializer _serializer;
    private readonly ILogger<ExceptionlessMcpTools> _logger;
    private readonly TimeProvider _timeProvider;

    public ExceptionlessMcpTools(
        IHttpContextAccessor httpContextAccessor,
        IOrganizationRepository organizationRepository,
        IProjectRepository projectRepository,
        IStackRepository stackRepository,
        IEventRepository eventRepository,
        StackQueryValidator stackQueryValidator,
        PersistentEventQueryValidator eventQueryValidator,
        SemanticVersionParser semanticVersionParser,
        ITextSerializer serializer,
        ILogger<ExceptionlessMcpTools> logger,
        TimeProvider timeProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _stackRepository = stackRepository;
        _eventRepository = eventRepository;
        _stackQueryValidator = stackQueryValidator;
        _eventQueryValidator = eventQueryValidator;
        _semanticVersionParser = semanticVersionParser;
        _serializer = serializer;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    [McpServerTool(Name = "list_projects", ReadOnly = true, UseStructuredContent = true)]
    [Description("Lists projects the authenticated Exceptionless user can access. When pagination.hasMore is true, pass pagination.after to fetch the next page or pagination.before to fetch the previous page.")]
    public async Task<McpResponse<McpListData<McpProjectResult>>> ListProjectsAsync(
        [Description(ProjectFilterDescription)]
        string? filter = null,
        [Description("Optional sort expression. Defaults to project name.")]
        string? sort = null,
        [Description(SummaryLimitDescription)]
        int limit = DefaultLimit,
        [Description("Optional cursor returned from a previous response. Fetches results after this cursor.")]
        string? after = null,
        [Description("Optional cursor returned from a previous response. Fetches results before this cursor.")]
        string? before = null)
    {
        try
        {
            EnsureScope(AuthorizationRoles.ProjectsRead);
            var validation = ValidateProjectSearch(filter, sort, limit);
            if (validation.Error is not null)
                return McpResponse<McpListData<McpProjectResult>>.Failed(validation.Error);

            if (!TryValidatePaginationCursors(after, before, out var cursorError))
                return McpResponse<McpListData<McpProjectResult>>.Failed(cursorError);

            int resolvedLimit = validation.Limit;

            var organizations = await GetAccessibleOrganizationsAsync();
            var systemFilter = new AppFilter(organizations)
            {
                IsUserOrganizationsFilter = true
            };

            var results = await _projectRepository.GetByFilterAsync(systemFilter, filter, sort, o => o
                .SearchBeforeToken(before, _serializer)
                .SearchAfterToken(after, _serializer)
                .PageLimit(resolvedLimit));

            return ToListResponse(results, ToProjectResult, validation.Warning, resolvedLimit);
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpListData<McpProjectResult>>.Failed(McpErrors.NotAccessible("Unable to list projects. No accessible organizations were found."));
        }
        catch (Exception ex) when (!String.IsNullOrWhiteSpace(filter) && IsExpectedToolError(ex))
        {
            return McpResponse<McpListData<McpProjectResult>>.Failed(McpErrors.InvalidFilter($"Invalid filter: {ex.Message}"));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<McpListData<McpProjectResult>>.Failed(McpErrors.QueryFailed("Unable to list projects. Check the filter, sort, and limit values."));
        }
    }

    [McpServerTool(Name = "get_project", ReadOnly = true, UseStructuredContent = true)]
    [Description("Gets summary details for a specific Exceptionless project.")]
    public async Task<McpResponse<McpProjectResult>> GetProjectAsync(
        [Description("The Exceptionless project id.")]
        string projectId)
    {
        try
        {
            EnsureScope(AuthorizationRoles.ProjectsRead);
            if (!TryValidateId(projectId, "projectId", out var idError))
                return McpResponse<McpProjectResult>.Failed(idError);

            var project = await GetAccessibleProjectAsync(projectId);
            return McpResponse<McpProjectResult>.Success(ToProjectResult(project));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpProjectResult>.Failed(ToLookupError("Project", projectId, ex));
        }
    }

    [McpServerTool(Name = "search_stacks", ReadOnly = true, UseStructuredContent = true)]
    [Description("Searches stacks in an Exceptionless project, useful for top issues, top 404s, or recent problem groups. When pagination.hasMore is true, pass pagination.after to fetch the next page or pagination.before to fetch the previous page.")]
    public async Task<McpResponse<McpListData<McpStackResult>>> SearchStacksAsync(
        [Description("The Exceptionless project id to search within.")]
        string projectId,
        [Description(StackFilterDescription)]
        string? filter = null,
        [Description("Optional sort expression. Defaults to -last_occurrence.")]
        string? sort = "-last_occurrence",
        [Description(SummaryLimitDescription)]
        int limit = DefaultLimit,
        [Description(LastDescription)]
        string? last = null,
        [Description(StartUtcDescription)]
        string? startUtc = null,
        [Description(EndUtcDescription)]
        string? endUtc = null,
        [Description("Optional cursor returned from a previous response. Fetches results after this cursor.")]
        string? after = null,
        [Description("Optional cursor returned from a previous response. Fetches results before this cursor.")]
        string? before = null)
    {
        try
        {
            EnsureScope(AuthorizationRoles.StacksRead);
            if (!TryValidateId(projectId, "projectId", out var idError))
                return McpResponse<McpListData<McpStackResult>>.Failed(idError);

            var validation = await ValidateSearchAsync(filter, sort, limit, StackFilterFields, StackSortFields, _stackQueryValidator);
            if (validation.Error is not null)
                return McpResponse<McpListData<McpStackResult>>.Failed(validation.Error);

            if (!TryValidatePaginationCursors(after, before, out var cursorError))
                return McpResponse<McpListData<McpStackResult>>.Failed(cursorError);

            if (!TryResolveTimeRange(last, startUtc, endUtc, out var timeRange, out var timeError))
                return McpResponse<McpListData<McpStackResult>>.Failed(timeError);

            var (project, organization) = await GetProjectAndOrganizationAsync(projectId);
            var systemFilter = new AppFilter(project, organization);

            var results = await _stackRepository.FindAsync(
                q => ApplyStackTimeRange(q.AppFilter(systemFilter).FilterExpression(filter).SortExpression(sort ?? "-last_occurrence"), timeRange),
                o => o
                    .SearchBeforeToken(before, _serializer)
                    .SearchAfterToken(after, _serializer)
                    .PageLimit(validation.Limit));

            return ToListResponse(results, ToStackResult, validation.Warning, validation.Limit);
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpListData<McpStackResult>>.Failed(ToLookupError("Project", projectId, ex));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<McpListData<McpStackResult>>.Failed(McpErrors.QueryFailed("Unable to search stacks. Check the project id, filter, sort, and limit values."));
        }
    }

    [McpServerTool(Name = "get_stack", ReadOnly = true, UseStructuredContent = true)]
    [Description("Gets summary details for a specific Exceptionless stack.")]
    public async Task<McpResponse<McpStackResult>> GetStackAsync(
        [Description("The Exceptionless stack id.")]
        string stackId)
    {
        try
        {
            EnsureScope(AuthorizationRoles.StacksRead);
            if (!TryValidateId(stackId, "stackId", out var idError))
                return McpResponse<McpStackResult>.Failed(idError);

            var stack = await GetAccessibleStackAsync(stackId);
            return McpResponse<McpStackResult>.Success(ToStackResult(stack));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpStackResult>.Failed(ToLookupError("Stack", stackId, ex));
        }
    }

    [McpServerTool(Name = "get_stack_events", ReadOnly = true, UseStructuredContent = true)]
    [Description("Lists recent events in a specific Exceptionless stack. When pagination.hasMore is true, pass pagination.after to fetch the next page or pagination.before to fetch the previous page.")]
    public async Task<McpResponse<McpListData<McpEventResult>>> GetStackEventsAsync(
        [Description("The Exceptionless stack id.")]
        string stackId,
        [Description(EventFilterDescription)]
        string? filter = null,
        [Description("Optional sort expression. Defaults to -date.")]
        string? sort = "-date",
        [Description(SummaryLimitDescription)]
        int limit = DefaultLimit,
        [Description(LastDescription)]
        string? last = null,
        [Description(StartUtcDescription)]
        string? startUtc = null,
        [Description(EndUtcDescription)]
        string? endUtc = null,
        [Description("Optional cursor returned from a previous response. Fetches results after this cursor.")]
        string? after = null,
        [Description("Optional cursor returned from a previous response. Fetches results before this cursor.")]
        string? before = null)
    {
        try
        {
            EnsureScope(AuthorizationRoles.EventsRead);
            if (!TryValidateId(stackId, "stackId", out var idError))
                return McpResponse<McpListData<McpEventResult>>.Failed(idError);

            var validation = await ValidateSearchAsync(filter, sort, limit, EventFilterFields, EventSortFields, _eventQueryValidator);
            if (validation.Error is not null)
                return McpResponse<McpListData<McpEventResult>>.Failed(validation.Error);

            if (!TryValidatePaginationCursors(after, before, out var cursorError))
                return McpResponse<McpListData<McpEventResult>>.Failed(cursorError);

            if (!TryResolveTimeRange(last, startUtc, endUtc, out var timeRange, out var timeError))
                return McpResponse<McpListData<McpEventResult>>.Failed(timeError);

            var (stack, organization) = await GetStackAndOrganizationAsync(stackId);
            var systemFilter = new AppFilter(stack, organization);

            var results = await _eventRepository.FindAsync(
                q => ApplyEventTimeRange(q.AppFilter(systemFilter).FilterExpression(filter).EnforceEventStackFilter().SortExpression(sort ?? "-date"), timeRange),
                o => o
                    .SearchBeforeToken(before, _serializer)
                    .SearchAfterToken(after, _serializer)
                    .PageLimit(validation.Limit));

            return ToListResponse(results, ev => ToEventResult(ev), validation.Warning, validation.Limit);
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpListData<McpEventResult>>.Failed(ToLookupError("Stack", stackId, ex));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<McpListData<McpEventResult>>.Failed(McpErrors.QueryFailed("Unable to list stack events. Check the stack id, filter, sort, and limit values."));
        }
    }

    [McpServerTool(Name = "search_events", ReadOnly = true, UseStructuredContent = true)]
    [Description("Searches event summary rows in an Exceptionless project. Use this for event-first triage across correlation ids, order ids, users, sessions, recent windows, or data.* fields. When pagination.hasMore is true, pass pagination.after or pagination.before to page.")]
    public async Task<McpResponse<McpListData<McpEventResult>>> SearchEventsAsync(
        [Description("The Exceptionless project id to search within.")]
        string projectId,
        [Description(EventFilterDescription)]
        string? filter = null,
        [Description("Optional sort expression. Defaults to -date.")]
        string? sort = "-date",
        [Description(SummaryLimitDescription)]
        int limit = DefaultLimit,
        [Description(LastDescription)]
        string? last = null,
        [Description(StartUtcDescription)]
        string? startUtc = null,
        [Description(EndUtcDescription)]
        string? endUtc = null,
        [Description("Optional cursor returned from a previous response. Fetches results after this cursor.")]
        string? after = null,
        [Description("Optional cursor returned from a previous response. Fetches results before this cursor.")]
        string? before = null)
    {
        try
        {
            EnsureScope(AuthorizationRoles.EventsRead);
            if (!TryValidateId(projectId, "projectId", out var idError))
                return McpResponse<McpListData<McpEventResult>>.Failed(idError);

            var validation = await ValidateSearchAsync(filter, sort, limit, EventFilterFields, EventSortFields, _eventQueryValidator);
            if (validation.Error is not null)
                return McpResponse<McpListData<McpEventResult>>.Failed(validation.Error);

            if (!TryValidatePaginationCursors(after, before, out var cursorError))
                return McpResponse<McpListData<McpEventResult>>.Failed(cursorError);

            if (!TryResolveTimeRange(last, startUtc, endUtc, out var timeRange, out var timeError))
                return McpResponse<McpListData<McpEventResult>>.Failed(timeError);

            var (project, organization) = await GetProjectAndOrganizationAsync(projectId);
            var systemFilter = new AppFilter(project, organization);

            var results = await _eventRepository.FindAsync(
                q => ApplyEventTimeRange(q.AppFilter(systemFilter).FilterExpression(filter).EnforceEventStackFilter().SortExpression(sort ?? "-date"), timeRange),
                o => o
                    .SearchBeforeToken(before, _serializer)
                    .SearchAfterToken(after, _serializer)
                    .PageLimit(validation.Limit));

            return ToListResponse(results, ev => ToEventResult(ev), validation.Warning, validation.Limit);
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpListData<McpEventResult>>.Failed(ToLookupError("Project", projectId, ex));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<McpListData<McpEventResult>>.Failed(McpErrors.QueryFailed("Unable to search events. Check the project id, filter, sort, limit, and time range values."));
        }
    }

    [McpServerTool(Name = "get_event", ReadOnly = true, UseStructuredContent = true)]
    [Description("Gets details for a specific Exceptionless event, including error, request, environment, and extended data when available.")]
    public async Task<McpResponse<McpEventResult>> GetEventAsync(
        [Description("The Exceptionless event id.")]
        string eventId,
        [Description("Whether to include error, request, environment, and extended data. Defaults to true.")]
        bool includeDetails = true,
        [Description("Maximum serialized detail payload size in bytes when includeDetails is true. Defaults to 16384, minimum 1024, maximum 65536. Large detail sections are omitted with truncation metadata.")]
        int maxDetailSize = DefaultMaxDetailSize)
    {
        try
        {
            EnsureScope(AuthorizationRoles.EventsRead);
            if (!TryValidateId(eventId, "eventId", out var idError))
                return McpResponse<McpEventResult>.Failed(idError);

            if (includeDetails && !TryValidateDetailSize(maxDetailSize, out var detailSizeError))
                return McpResponse<McpEventResult>.Failed(detailSizeError);

            var ev = await _eventRepository.GetByIdAsync(eventId, o => o.Cache());
            if (ev is null)
                return McpResponse<McpEventResult>.Failed(McpErrors.NotFound($"Event {eventId} was not found or is not accessible.", "eventId", eventId));

            EnsureOrganizationAccess(ev.OrganizationId);
            return McpResponse<McpEventResult>.Success(ToEventResult(ev, includeDetails, maxDetailSize));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpEventResult>.Failed(ToLookupError("Event", eventId, ex));
        }
    }

    [McpServerTool(Name = "count_events", ReadOnly = true, UseStructuredContent = true)]
    [Description("Counts Exceptionless events and occurrences in a project, with optional time buckets and groupBy dimensions for questions like occurrences by version, tag, user, or error type.")]
    public async Task<McpResponse<McpEventCountResult>> CountEventsAsync(
        [Description("The Exceptionless project id to count within.")]
        string projectId,
        [Description(EventFilterDescription)]
        string? filter = null,
        [Description(LastDescription)]
        string? last = null,
        [Description(StartUtcDescription)]
        string? startUtc = null,
        [Description(EndUtcDescription)]
        string? endUtc = null,
        [Description("Optional date histogram interval for trend buckets, such as 1h, 1d, or 1w. Leave blank for a total only.")]
        string? interval = null,
        [Description(EventGroupByDescription)]
        string? groupBy = null,
        [Description("Maximum number of groups to return when groupBy is specified. Defaults to 10 and is capped at 25.")]
        int groupLimit = DefaultGroupLimit)
    {
        try
        {
            EnsureScope(AuthorizationRoles.EventsRead);
            if (!TryValidateId(projectId, "projectId", out var idError))
                return McpResponse<McpEventCountResult>.Failed(idError);

            var validation = await ValidateSearchAsync(filter, sort: null, DefaultLimit, EventFilterFields, EventSortFields, _eventQueryValidator);
            if (validation.Error is not null)
                return McpResponse<McpEventCountResult>.Failed(validation.Error);

            if (!TryResolveTimeRange(last, startUtc, endUtc, out var timeRange, out var timeError))
                return McpResponse<McpEventCountResult>.Failed(timeError);

            if (!TryValidateInterval(interval, out var intervalError))
                return McpResponse<McpEventCountResult>.Failed(intervalError);

            if (!TryResolveEventGroupBy(groupBy, out var resolvedGroupBy, out var groupByError))
                return McpResponse<McpEventCountResult>.Failed(groupByError);

            if (!TryValidateGroupLimit(groupLimit, out int resolvedGroupLimit, out var groupLimitError, out var groupLimitWarning))
                return McpResponse<McpEventCountResult>.Failed(groupLimitError);

            var (project, organization) = await GetProjectAndOrganizationAsync(projectId);
            var systemFilter = new AppFilter(project, organization);
            string aggregations = BuildCountEventsAggregations(interval, resolvedGroupBy, resolvedGroupLimit);

            var aggregationValidation = await _eventQueryValidator.ValidateAggregationsAsync(aggregations);
            if (!aggregationValidation.IsValid)
                return McpResponse<McpEventCountResult>.Failed(McpErrors.InvalidGroupBy($"Invalid aggregation: {aggregationValidation.Message ?? "Unable to validate aggregation."}", groupBy, EventGroupByFields.Keys));

            var countQuery = ApplyEventTimeRange(new RepositoryQuery<PersistentEvent>().AppFilter(systemFilter), timeRange);
            var result = await _eventRepository.CountAsync(_ => countQuery
                .FilterExpression(filter)
                .EnforceEventStackFilter()
                .AggregationsExpression(aggregations));

            var histogram = String.IsNullOrWhiteSpace(interval) ? null : result.Aggregations.DateHistogram("date_date");
            var buckets = histogram?.Buckets?
                .Select(b => new McpEventTrendBucket(
                    b.Date.ToString("O", CultureInfo.InvariantCulture),
                    Convert.ToInt64(b.Total, CultureInfo.InvariantCulture),
                    GetNumericAggregationValue(b.Aggregations.Sum("sum_count")?.Value, Convert.ToDouble(b.Total, CultureInfo.InvariantCulture))))
                .ToArray() ?? [];

            IReadOnlyCollection<McpEventCountGroup> groups = [];
            if (resolvedGroupBy is not null)
            {
                var groupTerms = result.Aggregations.Terms<string>(GetTermsAggregationName(resolvedGroupBy));
                groups = groupTerms?.Buckets?
                    .Select(b =>
                    {
                        var groupHistogram = String.IsNullOrWhiteSpace(interval) ? null : b.Aggregations.DateHistogram("date_date");
                        var groupTrend = groupHistogram?.Buckets?
                            .Select(bucket => new McpEventTrendBucket(
                                bucket.Date.ToString("O", CultureInfo.InvariantCulture),
                                Convert.ToInt64(bucket.Total, CultureInfo.InvariantCulture),
                                GetNumericAggregationValue(bucket.Aggregations.Sum("sum_count")?.Value, Convert.ToDouble(bucket.Total, CultureInfo.InvariantCulture))))
                            .ToArray() ?? [];

                        return new McpEventCountGroup(
                            b.KeyAsString ?? b.Key?.ToString() ?? String.Empty,
                            Convert.ToInt64(b.Total, CultureInfo.InvariantCulture),
                            GetNumericAggregationValue(b.Aggregations.Sum("sum_count")?.Value, Convert.ToDouble(b.Total, CultureInfo.InvariantCulture)),
                            groupTrend);
                    })
                    .ToArray() ?? [];
            }

            return McpResponse<McpEventCountResult>.Success(new McpEventCountResult(
                result.Total,
                GetNumericAggregationValue(result.Aggregations.Sum("sum_count")?.Value, result.Total),
                Convert.ToInt64(result.Aggregations.Cardinality("cardinality_stack_id")?.Value.GetValueOrDefault() ?? 0, CultureInfo.InvariantCulture),
                Convert.ToInt64(result.Aggregations.Cardinality("cardinality_user")?.Value.GetValueOrDefault() ?? 0, CultureInfo.InvariantCulture),
                interval,
                timeRange.StartUtc,
                timeRange.EndUtc,
                buckets,
                resolvedGroupBy?.Name,
                groups),
                groupLimitWarning);
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpEventCountResult>.Failed(ToLookupError("Project", projectId, ex));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<McpEventCountResult>.Failed(McpErrors.QueryFailed("Unable to count events. Check the project id, filter, time range, and interval values."));
        }
    }

    [McpServerTool(Name = "update_stack_status", ReadOnly = false, UseStructuredContent = true)]
    [Description("Changes a stack status. Use status fixed with fixedInVersion to mark an issue fixed in a release, or use open, ignored, or discarded. Snoozed stacks must use snooze_stack.")]
    public async Task<McpResponse<McpStackUpdateResult>> UpdateStackStatusAsync(
        [Description("The Exceptionless stack id.")]
        string stackId,
        [Description("Target status: open, fixed, ignored, or discarded. Regressed and snoozed cannot be set directly.")]
        string status,
        [Description("Optional semantic version for fixed status, such as 1.0.2. Only allowed when status is fixed.")]
        string? fixedInVersion = null)
    {
        try
        {
            EnsureScope(AuthorizationRoles.StacksWrite);
            if (!TryValidateId(stackId, "stackId", out var idError))
                return McpResponse<McpStackUpdateResult>.Failed(idError);

            if (!TryParseWritableStackStatus(status, out var stackStatus, out var statusError))
                return McpResponse<McpStackUpdateResult>.Failed(statusError);

            if (!String.IsNullOrWhiteSpace(fixedInVersion) && stackStatus != StackStatus.Fixed)
                return McpResponse<McpStackUpdateResult>.Failed(McpErrors.InvalidVersion("fixedInVersion can only be used when status is fixed.", fixedInVersion));

            var stack = await GetAccessibleStackForWriteAsync(stackId);
            bool changed = stack.Status != stackStatus || (stackStatus == StackStatus.Fixed && !String.Equals(stack.FixedInVersion, NormalizeFixedVersion(fixedInVersion), StringComparison.Ordinal));

            if (stackStatus == StackStatus.Fixed)
            {
                var semanticVersion = String.IsNullOrWhiteSpace(fixedInVersion) ? null : _semanticVersionParser.Parse(fixedInVersion);
                if (!String.IsNullOrWhiteSpace(fixedInVersion) && semanticVersion is null)
                    return McpResponse<McpStackUpdateResult>.Failed(McpErrors.InvalidVersion("Invalid semantic version.", fixedInVersion));

                stack.MarkFixed(semanticVersion, _timeProvider);
            }
            else
            {
                stack.Status = stackStatus;
                stack.DateFixed = null;
                stack.FixedInVersion = null;
                stack.SnoozeUntilUtc = null;
            }

            await _stackRepository.SaveAsync(stack, o => o.ImmediateConsistency());
            return McpResponse<McpStackUpdateResult>.Success(new McpStackUpdateResult(
                ToStackResult(stack),
                changed,
                $"Stack {stack.Id} status is {stack.Status.ToString().ToLowerInvariant()}."));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpStackUpdateResult>.Failed(ToLookupError("Stack", stackId, ex));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<McpStackUpdateResult>.Failed(McpErrors.QueryFailed("Unable to update stack status. Check the stack id, status, and fixed version."));
        }
    }

    [McpServerTool(Name = "snooze_stack", ReadOnly = false, UseStructuredContent = true)]
    [Description("Snoozes a stack until a future UTC time or for a relative duration. Snoozing clears fixed metadata and sets the stack status to snoozed.")]
    public async Task<McpResponse<McpStackUpdateResult>> SnoozeStackAsync(
        [Description("The Exceptionless stack id.")]
        string stackId,
        [Description(SnoozeDurationDescription)]
        string? duration = null,
        [Description("Optional UTC time to snooze until, for example 2026-06-26T12:00:00Z. Do not combine with duration.")]
        string? snoozeUntilUtc = null)
    {
        try
        {
            EnsureScope(AuthorizationRoles.StacksWrite);
            if (!TryValidateId(stackId, "stackId", out var idError))
                return McpResponse<McpStackUpdateResult>.Failed(idError);

            if (!TryResolveSnoozeUntil(duration, snoozeUntilUtc, out var untilUtc, out var snoozeError))
                return McpResponse<McpStackUpdateResult>.Failed(snoozeError);

            var stack = await GetAccessibleStackForWriteAsync(stackId);
            bool changed = stack.Status != StackStatus.Snoozed || stack.SnoozeUntilUtc != untilUtc;
            stack.Status = StackStatus.Snoozed;
            stack.SnoozeUntilUtc = untilUtc;
            stack.FixedInVersion = null;
            stack.DateFixed = null;

            await _stackRepository.SaveAsync(stack, o => o.ImmediateConsistency());
            return McpResponse<McpStackUpdateResult>.Success(new McpStackUpdateResult(
                ToStackResult(stack),
                changed,
                $"Stack {stack.Id} is snoozed until {untilUtc:O}."));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpStackUpdateResult>.Failed(ToLookupError("Stack", stackId, ex));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<McpStackUpdateResult>.Failed(McpErrors.QueryFailed("Unable to snooze stack. Check the stack id and snooze duration."));
        }
    }

    [McpServerTool(Name = "set_stack_critical", ReadOnly = false, UseStructuredContent = true)]
    [Description("Controls whether future events for a stack are marked critical.")]
    public async Task<McpResponse<McpStackUpdateResult>> SetStackCriticalAsync(
        [Description("The Exceptionless stack id.")]
        string stackId,
        [Description("True marks future events for this stack as critical; false clears that behavior.")]
        bool critical)
    {
        try
        {
            EnsureScope(AuthorizationRoles.StacksWrite);
            if (!TryValidateId(stackId, "stackId", out var idError))
                return McpResponse<McpStackUpdateResult>.Failed(idError);

            var stack = await GetAccessibleStackForWriteAsync(stackId);
            bool changed = stack.OccurrencesAreCritical != critical;
            stack.OccurrencesAreCritical = critical;

            await _stackRepository.SaveAsync(stack, o => o.ImmediateConsistency());
            return McpResponse<McpStackUpdateResult>.Success(new McpStackUpdateResult(
                ToStackResult(stack),
                changed,
                critical
                    ? $"Future events for stack {stack.Id} will be marked critical."
                    : $"Future events for stack {stack.Id} will no longer be marked critical."));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpStackUpdateResult>.Failed(ToLookupError("Stack", stackId, ex));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<McpStackUpdateResult>.Failed(McpErrors.QueryFailed("Unable to update stack critical setting. Check the stack id."));
        }
    }

    [McpServerTool(Name = "add_stack_reference_link", ReadOnly = false, UseStructuredContent = true)]
    [Description("Adds a reference link to a stack. Use this to attach an external issue, pull request, deployment, or incident URL to an Exceptionless issue.")]
    public async Task<McpResponse<McpStackUpdateResult>> AddStackReferenceLinkAsync(
        [Description("The Exceptionless stack id.")]
        string stackId,
        [Description("The reference link to add to the stack, such as an issue, pull request, deployment, or incident URL.")]
        string url)
    {
        try
        {
            EnsureScope(AuthorizationRoles.StacksWrite);
            if (!TryValidateId(stackId, "stackId", out var idError))
                return McpResponse<McpStackUpdateResult>.Failed(idError);

            string? referenceLink = NormalizeReferenceLink(url);
            if (referenceLink is null)
                return McpResponse<McpStackUpdateResult>.Failed(McpErrors.InvalidReferenceLink("url is required.", url));

            var stack = await GetAccessibleStackForWriteAsync(stackId);
            bool changed = !stack.References.Contains(referenceLink);
            if (changed)
            {
                stack.References.Add(referenceLink);
                await _stackRepository.SaveAsync(stack, o => o.ImmediateConsistency());
            }

            return McpResponse<McpStackUpdateResult>.Success(new McpStackUpdateResult(
                ToStackResult(stack),
                changed,
                changed
                    ? $"Reference link was added to stack {stack.Id}."
                    : $"Stack {stack.Id} already has that reference link."));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpStackUpdateResult>.Failed(ToLookupError("Stack", stackId, ex));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<McpStackUpdateResult>.Failed(McpErrors.QueryFailed("Unable to add stack reference link. Check the stack id and url."));
        }
    }

    [McpServerTool(Name = "remove_stack_reference_link", ReadOnly = false, UseStructuredContent = true)]
    [Description("Removes a reference link from a stack.")]
    public async Task<McpResponse<McpStackUpdateResult>> RemoveStackReferenceLinkAsync(
        [Description("The Exceptionless stack id.")]
        string stackId,
        [Description("The reference link to remove from the stack.")]
        string url)
    {
        try
        {
            EnsureScope(AuthorizationRoles.StacksWrite);
            if (!TryValidateId(stackId, "stackId", out var idError))
                return McpResponse<McpStackUpdateResult>.Failed(idError);

            string? referenceLink = NormalizeReferenceLink(url);
            if (referenceLink is null)
                return McpResponse<McpStackUpdateResult>.Failed(McpErrors.InvalidReferenceLink("url is required.", url));

            var stack = await GetAccessibleStackForWriteAsync(stackId);
            bool changed = stack.References.Remove(referenceLink);
            if (changed)
                await _stackRepository.SaveAsync(stack, o => o.ImmediateConsistency());

            return McpResponse<McpStackUpdateResult>.Success(new McpStackUpdateResult(
                ToStackResult(stack),
                changed,
                changed
                    ? $"Reference link was removed from stack {stack.Id}."
                    : $"Stack {stack.Id} did not have that reference link."));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpStackUpdateResult>.Failed(ToLookupError("Stack", stackId, ex));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<McpStackUpdateResult>.Failed(McpErrors.QueryFailed("Unable to remove stack reference link. Check the stack id and url."));
        }
    }

    [McpServerTool(Name = "get_filter_fields", ReadOnly = true, UseStructuredContent = true)]
    [Description("Lists supported Exceptionless MCP filter and sort fields for projects, stacks, and events. Dynamic data.* and idx.* filter fields are allowed for stacks and events.")]
    public McpResponse<McpFilterFieldsResult> GetFilterFields()
    {
        try
        {
            EnsureScope(AuthorizationRoles.McpRead);
            return McpResponse<McpFilterFieldsResult>.Success(new McpFilterFieldsResult(
                ToFilterFieldSet(ProjectFilterFields, ProjectSortFields),
                ToFilterFieldSet(StackFilterFields, StackSortFields, "data.", "idx."),
                ToFilterFieldSet(EventFilterFields, EventSortFields, "data.", "idx.")));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpFilterFieldsResult>.Failed(ToLookupError("Filter metadata", "current user", ex));
        }
    }

    private HttpRequest Request => _httpContextAccessor.HttpContext?.Request
        ?? throw new UnauthorizedAccessException("No active request is available.");

    private void EnsureScope(string scope)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
            throw new UnauthorizedAccessException("No authenticated user is available.");

        if (user.HasClaim(ClaimTypes.Role, AuthorizationRoles.User) || user.HasClaim(ClaimTypes.Role, scope))
            return;

        throw new McpForbiddenException($"Missing required scope {scope}.", scope);
    }

    private async Task<IReadOnlyCollection<Organization>> GetAccessibleOrganizationsAsync()
    {
        var organizationIds = Request.GetAssociatedOrganizationIds();
        if (organizationIds.Count == 0)
            throw new UnauthorizedAccessException("No organizations are associated with the current user.");

        var organizations = await _organizationRepository.GetByIdsAsync(organizationIds.ToArray(), o => o.Cache());
        return organizations.ToArray();
    }

    private async Task<Project> GetAccessibleProjectAsync(string projectId)
    {
        if (String.IsNullOrWhiteSpace(projectId))
            throw new ArgumentException("Project id is required.", nameof(projectId));

        var project = await _projectRepository.GetByIdAsync(projectId, o => o.Cache());
        if (project is null)
            throw new KeyNotFoundException($"Project {projectId} was not found.");

        EnsureOrganizationAccess(project.OrganizationId);
        return project;
    }

    private async Task<(Project Project, Organization Organization)> GetProjectAndOrganizationAsync(string projectId)
    {
        var project = await GetAccessibleProjectAsync(projectId);
        var organization = await _organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
        if (organization is null)
            throw new KeyNotFoundException($"Organization {project.OrganizationId} was not found.");

        return (project, organization);
    }

    private async Task<Stack> GetAccessibleStackAsync(string stackId)
    {
        if (String.IsNullOrWhiteSpace(stackId))
            throw new ArgumentException("Stack id is required.", nameof(stackId));

        var stack = await _stackRepository.GetByIdAsync(stackId, o => o.Cache());
        if (stack is null)
            throw new KeyNotFoundException($"Stack {stackId} was not found.");

        EnsureOrganizationAccess(stack.OrganizationId);
        return stack;
    }

    private async Task<Stack> GetAccessibleStackForWriteAsync(string stackId)
    {
        if (String.IsNullOrWhiteSpace(stackId))
            throw new ArgumentException("Stack id is required.", nameof(stackId));

        var stack = await _stackRepository.GetByIdAsync(stackId, o => o.Cache(false));
        if (stack is null)
            throw new KeyNotFoundException($"Stack {stackId} was not found.");

        EnsureOrganizationAccess(stack.OrganizationId);
        return stack;
    }

    private async Task<(Stack Stack, Organization Organization)> GetStackAndOrganizationAsync(string stackId)
    {
        var stack = await GetAccessibleStackAsync(stackId);
        var organization = await _organizationRepository.GetByIdAsync(stack.OrganizationId, o => o.Cache());
        if (organization is null)
            throw new KeyNotFoundException($"Organization {stack.OrganizationId} was not found.");

        return (stack, organization);
    }

    private void EnsureOrganizationAccess(string organizationId)
    {
        if (!Request.CanAccessOrganization(organizationId))
            throw new UnauthorizedAccessException("The current user cannot access the requested organization.");
    }

    private static bool TryValidateLimit(int limit, out int resolvedLimit, out string? error, out string? warning)
    {
        resolvedLimit = limit;
        warning = null;

        if (limit <= 0)
        {
            error = $"Limit must be between 1 and {MaxSummaryLimit}.";
            return false;
        }

        if (limit > MaxSummaryLimit)
        {
            resolvedLimit = MaxSummaryLimit;
            warning = $"Limit was capped at {MaxSummaryLimit}.";
        }

        error = null;
        return true;
    }

    private static SearchValidationResult ValidateProjectSearch(string? filter, string? sort, int limit)
    {
        if (!TryValidateLimit(limit, out int resolvedLimit, out string? limitError, out string? warning))
            return SearchValidationResult.Failed(McpErrors.InvalidLimit(limitError ?? "Invalid limit.", limit, MaxSummaryLimit));

        if (!TryValidateSort(sort, ProjectSortFields, out string? sortError))
            return SearchValidationResult.Failed(McpErrors.InvalidSort(sortError ?? "Invalid sort.", sort, ProjectSortFields));

        string? unknownField = GetUnknownFilterField(filter, ProjectFilterFields);
        if (unknownField is not null)
            return SearchValidationResult.Failed(McpErrors.UnknownFilterField($"Unknown filter field '{unknownField}'.", unknownField, ProjectFilterFields));

        return new SearchValidationResult(resolvedLimit, warning);
    }

    private async Task<SearchValidationResult> ValidateSearchAsync(
        string? filter,
        string? sort,
        int limit,
        IReadOnlySet<string> allowedFilterFields,
        IReadOnlySet<string> allowedSortFields,
        AppQueryValidator queryValidator)
    {
        if (!TryValidateLimit(limit, out int resolvedLimit, out string? limitError, out string? warning))
            return SearchValidationResult.Failed(McpErrors.InvalidLimit(limitError ?? "Invalid limit.", limit, MaxSummaryLimit));

        if (!TryValidateSort(sort, allowedSortFields, out string? sortError))
            return SearchValidationResult.Failed(McpErrors.InvalidSort(sortError ?? "Invalid sort.", sort, allowedSortFields));

        var queryValidation = await queryValidator.ValidateQueryAsync(filter);
        if (!queryValidation.IsValid)
            return SearchValidationResult.Failed(McpErrors.InvalidFilter($"Invalid filter: {queryValidation.Message ?? "Unable to parse filter."}"));

        string? unknownField = GetUnknownFilterField(filter, allowedFilterFields);
        if (unknownField is not null)
            return SearchValidationResult.Failed(McpErrors.UnknownFilterField($"Unknown filter field '{unknownField}'.", unknownField, allowedFilterFields));

        return new SearchValidationResult(resolvedLimit, warning);
    }

    private static bool TryValidateSort(string? sort, IReadOnlySet<string> allowedSortFields, out string? error)
    {
        if (String.IsNullOrWhiteSpace(sort))
        {
            error = null;
            return true;
        }

        foreach (string term in sort.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string field = term.TrimStart('+', '-');
            if (field.Length == 0 || !allowedSortFields.Contains(field))
            {
                error = $"Unknown sort field '{field}'. Allowed sort fields: {String.Join(", ", allowedSortFields.Order(StringComparer.OrdinalIgnoreCase))}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private bool TryValidatePaginationCursors(string? after, string? before, out McpErrorInfo error)
    {
        if (!TryValidatePaginationCursor("after", after, out error))
            return false;

        if (!TryValidatePaginationCursor("before", before, out error))
            return false;

        error = null!;
        return true;
    }

    private bool TryValidatePaginationCursor(string field, string? cursor, out McpErrorInfo error)
    {
        if (String.IsNullOrWhiteSpace(cursor))
        {
            error = null!;
            return true;
        }

        try
        {
            string json = DecodeCursor(cursor.Trim());
            var sortValues = _serializer.Deserialize<object[]>(json);
            if (sortValues is { Length: > 0 })
            {
                error = null!;
                return true;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or JsonException or NotSupportedException)
        {
            // Invalid cursor tokens should be reported as client input errors, not query failures.
        }

        error = McpErrors.InvalidCursor($"{field} is not a valid pagination cursor.", field);
        return false;
    }

    private static string DecodeCursor(string cursor)
    {
        cursor = cursor.Replace('_', '/').Replace('-', '+');
        switch (cursor.Length % 4)
        {
            case 2:
                cursor += "==";
                break;
            case 3:
                cursor += "=";
                break;
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
    }

    private bool TryResolveTimeRange(string? last, string? startUtc, string? endUtc, out McpTimeRange timeRange, out McpErrorInfo error)
    {
        timeRange = new McpTimeRange(null, null);

        if (!String.IsNullOrWhiteSpace(last) && (!String.IsNullOrWhiteSpace(startUtc) || !String.IsNullOrWhiteSpace(endUtc)))
        {
            error = McpErrors.InvalidTimeRange("Use either last or startUtc/endUtc, not both.", last, startUtc, endUtc);
            return false;
        }

        if (!String.IsNullOrWhiteSpace(last))
        {
            if (!TryParseRelativeTime(last, out var duration))
            {
                error = McpErrors.InvalidTimeRange("last must be a relative duration such as 30m, 24h, 7d, or 1w.", last, startUtc, endUtc);
                return false;
            }

            var end = _timeProvider.GetUtcNow().UtcDateTime;
            timeRange = new McpTimeRange(end.Subtract(duration), end);
            error = null!;
            return true;
        }

        if (!TryParseUtcDate(startUtc, out DateTime? start, out error))
            return false;

        if (!TryParseUtcDate(endUtc, out DateTime? endUtcDate, out error))
            return false;

        if (start.HasValue && endUtcDate.HasValue && start.Value >= endUtcDate.Value)
        {
            error = McpErrors.InvalidTimeRange("startUtc must be before endUtc.", last, startUtc, endUtc);
            return false;
        }

        timeRange = new McpTimeRange(start, endUtcDate);
        error = null!;
        return true;
    }

    private static bool TryParseRelativeTime(string value, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        var match = RelativeTimeRegex.Match(value.Trim());
        if (!match.Success || !Int32.TryParse(match.Groups["value"].Value, CultureInfo.InvariantCulture, out int amount) || amount <= 0)
            return false;

        duration = match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "m" => TimeSpan.FromMinutes(amount),
            "h" => TimeSpan.FromHours(amount),
            "d" => TimeSpan.FromDays(amount),
            "w" => TimeSpan.FromDays(amount * 7),
            _ => TimeSpan.Zero
        };

        return duration > TimeSpan.Zero;
    }

    private static bool TryParseUtcDate(string? value, out DateTime? date, out McpErrorInfo error)
    {
        date = null;
        if (String.IsNullOrWhiteSpace(value))
        {
            error = null!;
            return true;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
        {
            date = dto.UtcDateTime;
            error = null!;
            return true;
        }

        error = McpErrors.InvalidTimeRange($"{value} is not a valid UTC date/time.", last: null, startUtc: value, endUtc: null);
        return false;
    }

    private static bool TryValidateInterval(string? interval, out McpErrorInfo error)
    {
        if (String.IsNullOrWhiteSpace(interval) || IntervalRegex.IsMatch(interval))
        {
            error = null!;
            return true;
        }

        error = McpErrors.InvalidInterval("interval must be a duration such as 1h, 1d, 1w, or 1M.", interval);
        return false;
    }

    private static bool TryResolveEventGroupBy(string? groupBy, out McpEventGroupBy? resolvedGroupBy, out McpErrorInfo error)
    {
        resolvedGroupBy = null;
        if (String.IsNullOrWhiteSpace(groupBy))
        {
            error = null!;
            return true;
        }

        if (EventGroupByFields.TryGetValue(groupBy.Trim(), out resolvedGroupBy))
        {
            error = null!;
            return true;
        }

        error = McpErrors.InvalidGroupBy($"Unsupported groupBy field '{groupBy}'.", groupBy, EventGroupByFields.Keys);
        return false;
    }

    private static bool TryValidateGroupLimit(int groupLimit, out int resolvedGroupLimit, out McpErrorInfo error, out string? warning)
    {
        resolvedGroupLimit = groupLimit;
        warning = null;
        if (groupLimit <= 0)
        {
            error = McpErrors.InvalidLimit($"groupLimit must be between 1 and {MaxGroupLimit}.", groupLimit, MaxGroupLimit);
            return false;
        }

        if (groupLimit > MaxGroupLimit)
        {
            resolvedGroupLimit = MaxGroupLimit;
            warning = $"groupLimit was capped at {MaxGroupLimit}.";
        }

        error = null!;
        return true;
    }

    private static string BuildCountEventsAggregations(string? interval, McpEventGroupBy? groupBy, int groupLimit)
    {
        string rootAggregations = String.IsNullOrWhiteSpace(interval)
            ? "sum:count~1 cardinality:stack_id cardinality:user"
            : $"date:(date~{interval} sum:count~1) sum:count~1 cardinality:stack_id cardinality:user";

        if (groupBy is null)
            return rootAggregations;

        string groupAggregations = String.IsNullOrWhiteSpace(interval)
            ? "sum:count~1"
            : $"sum:count~1 date:(date~{interval} sum:count~1)";

        return $"terms:({groupBy.AggregationField}~{groupLimit} {groupAggregations}) {rootAggregations}";
    }

    private static string GetTermsAggregationName(McpEventGroupBy groupBy)
    {
        return $"terms_{groupBy.AggregationField}";
    }

    private bool TryResolveSnoozeUntil(string? duration, string? snoozeUntilUtc, out DateTime untilUtc, out McpErrorInfo error)
    {
        untilUtc = default;
        bool hasDuration = !String.IsNullOrWhiteSpace(duration);
        bool hasSnoozeUntilUtc = !String.IsNullOrWhiteSpace(snoozeUntilUtc);

        if (hasDuration == hasSnoozeUntilUtc)
        {
            error = McpErrors.InvalidSnooze("Specify exactly one of duration or snoozeUntilUtc.", duration, snoozeUntilUtc);
            return false;
        }

        if (hasDuration)
        {
            if (!TryParseRelativeTime(duration!, out var parsedDuration))
            {
                error = McpErrors.InvalidSnooze("duration must be a relative duration such as 30m, 2h, 3d, or 1w.", duration, snoozeUntilUtc);
                return false;
            }

            untilUtc = _timeProvider.GetUtcNow().UtcDateTime.Add(parsedDuration);
        }
        else
        {
            if (!DateTimeOffset.TryParse(snoozeUntilUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            {
                error = McpErrors.InvalidSnooze("snoozeUntilUtc must be a valid UTC date/time.", duration, snoozeUntilUtc);
                return false;
            }

            untilUtc = dto.UtcDateTime;
        }

        if (untilUtc < _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(5))
        {
            error = McpErrors.InvalidSnooze("Must snooze for at least 5 minutes.", duration, snoozeUntilUtc);
            return false;
        }

        error = null!;
        return true;
    }

    private static bool TryParseWritableStackStatus(string status, out StackStatus stackStatus, out McpErrorInfo error)
    {
        stackStatus = default;
        if (Enum.TryParse(status, ignoreCase: true, out stackStatus) && stackStatus is StackStatus.Open or StackStatus.Fixed or StackStatus.Ignored or StackStatus.Discarded)
        {
            error = null!;
            return true;
        }

        error = McpErrors.InvalidStatus("status must be one of: open, fixed, ignored, discarded.", status);
        return false;
    }

    private static string? NormalizeFixedVersion(string? fixedInVersion)
    {
        return String.IsNullOrWhiteSpace(fixedInVersion) ? null : fixedInVersion.Trim();
    }

    private static string? NormalizeReferenceLink(string? url)
    {
        return String.IsNullOrWhiteSpace(url) ? null : url.Trim();
    }

    private static bool TryValidateId(string id, string fieldName, out McpErrorInfo error)
    {
        if (!String.IsNullOrWhiteSpace(id) && IdRegex.IsMatch(id))
        {
            error = null!;
            return true;
        }

        error = McpErrors.InvalidId($"{fieldName} must be a 24 to 36 character alphanumeric Exceptionless id.", fieldName, id);
        return false;
    }

    private static bool TryValidateDetailSize(int maxDetailSize, out McpErrorInfo error)
    {
        if (maxDetailSize is >= MinDetailSize and <= MaxDetailSize)
        {
            error = null!;
            return true;
        }

        error = McpErrors.InvalidDetailSize($"maxDetailSize must be between {MinDetailSize} and {MaxDetailSize}.", maxDetailSize, MinDetailSize, MaxDetailSize);
        return false;
    }

    private static string? GetUnknownFilterField(string? filter, IReadOnlySet<string> allowedFilterFields)
    {
        if (String.IsNullOrWhiteSpace(filter))
            return null;

        foreach (Match match in FilterFieldRegex.Matches(filter))
        {
            string field = match.Groups["field"].Value;
            if (field.StartsWith("data.", StringComparison.OrdinalIgnoreCase) || field.StartsWith("idx.", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!allowedFilterFields.Contains(field))
                return field;
        }

        return null;
    }

    private static bool IsLookupError(Exception ex)
    {
        return ex is ArgumentException or KeyNotFoundException or UnauthorizedAccessException;
    }

    private static bool IsExpectedToolError(Exception ex)
    {
        return ex is ArgumentException or FormatException or InvalidOperationException or JsonException || ex.GetType().Name == "QueryValidationException";
    }

    private static McpErrorInfo ToLookupError(string resourceName, string resourceId, Exception ex)
    {
        string message = $"{resourceName} {resourceId} was not found or is not accessible.";

        return ex switch
        {
            ArgumentException => McpErrors.InvalidId($"{resourceName} id is invalid.", $"{resourceName.ToLowerInvariant()}Id", resourceId),
            McpForbiddenException forbidden => McpErrors.Forbidden(forbidden.Message, forbidden.RequiredScope),
            UnauthorizedAccessException => McpErrors.NotAccessible(message, resourceName, resourceId),
            KeyNotFoundException => McpErrors.NotFound(message, $"{resourceName.ToLowerInvariant()}Id", resourceId),
            _ => McpErrors.QueryFailed(message)
        };
    }

    private static McpProjectResult ToProjectResult(Project project)
    {
        return new McpProjectResult(
            project.Id,
            project.OrganizationId,
            project.Name,
            project.IsConfigured,
            project.LastEventDateUtc,
            project.CreatedUtc,
            project.UpdatedUtc,
            $"/api/v2/projects/{project.Id}");
    }

    private static McpStackResult ToStackResult(Stack stack)
    {
        return new McpStackResult(
            stack.Id,
            stack.OrganizationId,
            stack.ProjectId,
            stack.Type,
            stack.Status.ToString().ToLowerInvariant(),
            stack.Title,
            stack.Description,
            stack.TotalOccurrences,
            stack.FirstOccurrence,
            stack.LastOccurrence,
            stack.DateFixed,
            stack.FixedInVersion,
            stack.SnoozeUntilUtc,
            ToTags(stack.Tags),
            stack.References.ToArray(),
            stack.OccurrencesAreCritical,
            stack.CreatedUtc,
            stack.UpdatedUtc,
            $"/api/v2/stacks/{stack.Id}");
    }

    private McpEventResult ToEventResult(PersistentEvent ev, bool includeDetails = false, int maxDetailSize = DefaultMaxDetailSize)
    {
        return new McpEventResult(
            ev.Id,
            ev.OrganizationId,
            ev.ProjectId,
            ev.StackId,
            ev.Type,
            ev.Source,
            ev.Message,
            ev.Date,
            ToTags(ev.Tags),
            ev.ReferenceId,
            ev.IsFirstOccurrence,
            ev.CreatedUtc,
            $"/api/v2/events/{ev.Id}",
            includeDetails ? ToEventDetails(ev, maxDetailSize) : null);
    }

    private McpEventDetails ToEventDetails(PersistentEvent ev, int maxDetailSize)
    {
        var details = new McpEventDetails(
            ev.GetError(_serializer, _logger) ?? (object?)ev.GetSimpleError(_serializer, _logger),
            ev.GetRequestInfo(_serializer, _logger),
            ev.GetEnvironmentInfo(_serializer, _logger),
            ev.Data);

        return ApplyDetailLimit(details, maxDetailSize);
    }

    private McpEventDetails ApplyDetailLimit(McpEventDetails details, int maxDetailSize)
    {
        int originalSize = GetSerializedSize(details);
        if (originalSize <= maxDetailSize)
            return details with { Size = originalSize, MaxSize = maxDetailSize };

        var withoutData = details with
        {
            Data = null,
            IsTruncated = true,
            Size = originalSize,
            MaxSize = maxDetailSize,
            TruncationMessage = $"Extended data was omitted because event details exceeded maxDetailSize ({maxDetailSize} bytes). Retry with a larger maxDetailSize up to {MaxDetailSize}."
        };

        if (GetSerializedSize(withoutData) <= maxDetailSize)
            return withoutData;

        return new McpEventDetails(
            null,
            null,
            null,
            null,
            true,
            originalSize,
            maxDetailSize,
            $"Event detail fields were omitted because event details exceeded maxDetailSize ({maxDetailSize} bytes). Retry with a larger maxDetailSize up to {MaxDetailSize}.");
    }

    private int GetSerializedSize<T>(T value)
    {
        string json = _serializer.SerializeToString(value) ?? String.Empty;
        return Encoding.UTF8.GetByteCount(json);
    }

    private static string[] ToTags(IEnumerable<string?>? tags)
    {
        return tags?
            .Where(t => !String.IsNullOrEmpty(t))
            .Select(t => t!)
            .ToArray() ?? [];
    }

    private static McpFilterFieldSet ToFilterFieldSet(IReadOnlySet<string> filterFields, IReadOnlySet<string> sortFields, params string[] dynamicFilterPrefixes)
    {
        return new McpFilterFieldSet(
            filterFields.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            sortFields.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            dynamicFilterPrefixes);
    }

    private McpResponse<McpListData<TResult>> ToListResponse<TDocument, TResult>(
        FindResults<TDocument> results,
        Func<TDocument, TResult> mapper,
        string? warning,
        int limit)
        where TDocument : class
    {
        return McpResponse<McpListData<TResult>>.Success(
            new McpListData<TResult>(results.Documents.Select(mapper).ToArray()),
            warning,
            new McpPagination(
                results.HasMore,
                results.Hits.FirstOrDefault()?.GetSortToken(_serializer),
                results.HasMore ? results.Hits.LastOrDefault()?.GetSortToken(_serializer) : null,
                limit));
    }

    private static IRepositoryQuery<Stack> ApplyStackTimeRange(IRepositoryQuery<Stack> query, McpTimeRange timeRange)
    {
        return timeRange.HasRange
            ? query.DateRange(timeRange.StartUtc, timeRange.EndUtc, (Stack s) => s.LastOccurrence)
            : query;
    }

    private static IRepositoryQuery<PersistentEvent> ApplyEventTimeRange(IRepositoryQuery<PersistentEvent> query, McpTimeRange timeRange)
    {
        return timeRange.HasRange
            ? query.DateRange(timeRange.StartUtc, timeRange.EndUtc, (PersistentEvent e) => e.Date).Index(timeRange.StartUtc, timeRange.EndUtc)
            : query;
    }

    private static double GetNumericAggregationValue(object? value, double defaultValue)
    {
        return value is null ? defaultValue : Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    private static readonly Regex FilterFieldRegex = new(@"(?:^|[\s(])(?<field>@?[A-Za-z_][A-Za-z0-9_@.-]*):", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex IdRegex = new(@"^[A-Za-z0-9]{24,36}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RelativeTimeRegex = new(@"^(?<value>\d+)(?<unit>[mhdw])$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex IntervalRegex = new(@"^\d+[mhdwM]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<string, McpEventGroupBy> EventGroupByFields = new Dictionary<string, McpEventGroupBy>(StringComparer.OrdinalIgnoreCase)
    {
        ["version"] = new("version", EventIndex.Alias.Version),
        ["type"] = new("type", EventIndex.Alias.Type),
        ["source"] = new("source", EventIndex.Alias.Source),
        ["status"] = new("status", "status"),
        ["tag"] = new("tag", "tags"),
        ["tags"] = new("tag", "tags"),
        ["stack"] = new("stack", EventIndex.Alias.StackId),
        ["stack_id"] = new("stack", "stack_id"),
        ["user"] = new("user", EventIndex.Alias.User),
        ["level"] = new("level", EventIndex.Alias.Level),
        ["error.type"] = new("error.type", EventIndex.Alias.ErrorType),
        ["error.code"] = new("error.code", EventIndex.Alias.ErrorCode),
        ["os"] = new("os", "os"),
        ["os.version"] = new("os.version", "os.version"),
        ["browser"] = new("browser", EventIndex.Alias.Browser)
    };

    private static readonly HashSet<string> ProjectSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "created_utc",
        "updated_utc",
        "last_event_date_utc"
    };

    private static readonly HashSet<string> ProjectFilterFields = new(ProjectSortFields, StringComparer.OrdinalIgnoreCase)
    {
        "id",
        "organization_id"
    };

    private static readonly HashSet<string> StackSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        StackIndex.Alias.FirstOccurrence,
        "first_occurrence",
        StackIndex.Alias.LastOccurrence,
        "last_occurrence",
        StackIndex.Alias.TotalOccurrences,
        "total_occurrences",
        StackIndex.Alias.Type,
        StackIndex.Alias.Status,
        StackIndex.Alias.DateFixed,
        "date_fixed",
        StackIndex.Alias.OccurrencesAreCritical,
        "occurrences_are_critical",
        "created_utc",
        "updated_utc"
    };

    private static readonly HashSet<string> StackFilterFields = new(StackSortFields, StringComparer.OrdinalIgnoreCase)
    {
        StackIndex.Alias.Stack,
        StackIndex.Alias.OrganizationId,
        "organization_id",
        StackIndex.Alias.ProjectId,
        "project_id",
        StackIndex.Alias.SignatureHash,
        "signature_hash",
        StackIndex.Alias.Title,
        StackIndex.Alias.Description,
        StackIndex.Alias.Tags,
        "tags",
        StackIndex.Alias.References,
        StackIndex.Alias.IsFixed,
        StackIndex.Alias.FixedInVersion,
        "fixed_in_version",
        StackIndex.Alias.IsHidden,
        StackIndex.Alias.IsRegressed,
        "error"
    };

    private static readonly HashSet<string> EventSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        EventIndex.Alias.Date,
        EventIndex.Alias.Type,
        EventIndex.Alias.Source,
        EventIndex.Alias.Message,
        EventIndex.Alias.Value,
        EventIndex.Alias.Count,
        EventIndex.Alias.IsFirstOccurrence,
        "is_first_occurrence",
        EventIndex.Alias.ReferenceId,
        "reference_id",
        "created_utc"
    };

    private static readonly HashSet<string> EventFilterFields = new(EventSortFields, StringComparer.OrdinalIgnoreCase)
    {
        EventIndex.Alias.OrganizationId,
        "organization_id",
        EventIndex.Alias.ProjectId,
        "project_id",
        EventIndex.Alias.StackId,
        "stack_id",
        EventIndex.Alias.Id,
        EventIndex.Alias.Tags,
        "tags",
        EventIndex.Alias.Geo,
        EventIndex.Alias.IDX,
        EventIndex.Alias.Version,
        EventIndex.Alias.Level,
        EventIndex.Alias.SubmissionMethod,
        EventIndex.Alias.IpAddress,
        EventIndex.Alias.RequestUserAgent,
        EventIndex.Alias.RequestPath,
        EventIndex.Alias.Browser,
        EventIndex.Alias.BrowserVersion,
        EventIndex.Alias.BrowserMajorVersion,
        EventIndex.Alias.RequestIsBot,
        EventIndex.Alias.ClientVersion,
        EventIndex.Alias.ClientUserAgent,
        EventIndex.Alias.Device,
        EventIndex.Alias.OperatingSystem,
        EventIndex.Alias.OperatingSystemVersion,
        EventIndex.Alias.OperatingSystemMajorVersion,
        EventIndex.Alias.CommandLine,
        EventIndex.Alias.MachineName,
        EventIndex.Alias.MachineArchitecture,
        EventIndex.Alias.User,
        EventIndex.Alias.UserName,
        EventIndex.Alias.UserEmail,
        EventIndex.Alias.UserDescription,
        EventIndex.Alias.LocationCountry,
        EventIndex.Alias.LocationLevel1,
        EventIndex.Alias.LocationLevel2,
        EventIndex.Alias.LocationLocality,
        EventIndex.Alias.Error,
        EventIndex.Alias.ErrorCode,
        EventIndex.Alias.ErrorType,
        EventIndex.Alias.ErrorMessage,
        EventIndex.Alias.ErrorTargetType,
        EventIndex.Alias.ErrorTargetMethod,
        "status"
    };

    private sealed record SearchValidationResult(int Limit, string? Warning = null, McpErrorInfo? Error = null)
    {
        public static SearchValidationResult Failed(McpErrorInfo error)
        {
            return new SearchValidationResult(DefaultLimit, Error: error);
        }
    }

    private sealed record McpEventGroupBy(string Name, string AggregationField);
}

public static class McpErrorCodes
{
    public const string Forbidden = "forbidden";
    public const string InvalidCursor = "invalid_cursor";
    public const string InvalidDetailSize = "invalid_detail_size";
    public const string InvalidFilter = "invalid_filter";
    public const string InvalidGroupBy = "invalid_group_by";
    public const string InvalidId = "invalid_id";
    public const string InvalidInterval = "invalid_interval";
    public const string InvalidLimit = "invalid_limit";
    public const string InvalidReferenceLink = "invalid_reference_link";
    public const string InvalidSnooze = "invalid_snooze";
    public const string InvalidSort = "invalid_sort";
    public const string InvalidStatus = "invalid_status";
    public const string InvalidTimeRange = "invalid_time_range";
    public const string InvalidVersion = "invalid_version";
    public const string NotAccessible = "not_accessible";
    public const string NotFound = "not_found";
    public const string QueryFailed = "query_failed";
    public const string UnknownFilterField = "unknown_filter_field";
}

public static class McpErrors
{
    public static McpErrorInfo Forbidden(string message, string requiredScope)
    {
        return new McpErrorInfo(McpErrorCodes.Forbidden, message, new Dictionary<string, object?>
        {
            ["requiredScope"] = requiredScope
        });
    }

    public static McpErrorInfo InvalidCursor(string message, string field)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidCursor, message, new Dictionary<string, object?>
        {
            ["field"] = field
        });
    }

    public static McpErrorInfo InvalidDetailSize(string message, int value, int min, int max)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidDetailSize, message, new Dictionary<string, object?>
        {
            ["value"] = value,
            ["min"] = min,
            ["max"] = max
        });
    }

    public static McpErrorInfo InvalidFilter(string message)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidFilter, message);
    }

    public static McpErrorInfo InvalidGroupBy(string message, string? groupBy, IEnumerable<string> allowedFields)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidGroupBy, message, new Dictionary<string, object?>
        {
            ["groupBy"] = groupBy,
            ["allowedFields"] = allowedFields.Order(StringComparer.OrdinalIgnoreCase).ToArray()
        });
    }

    public static McpErrorInfo InvalidId(string message, string field, string? value)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidId, message, new Dictionary<string, object?>
        {
            ["field"] = field,
            ["value"] = value
        });
    }

    public static McpErrorInfo InvalidInterval(string message, string? interval)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidInterval, message, new Dictionary<string, object?>
        {
            ["interval"] = interval
        });
    }

    public static McpErrorInfo InvalidLimit(string message, int value, int max)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidLimit, message, new Dictionary<string, object?>
        {
            ["value"] = value,
            ["min"] = 1,
            ["max"] = max
        });
    }

    public static McpErrorInfo InvalidReferenceLink(string message, string? url)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidReferenceLink, message, new Dictionary<string, object?>
        {
            ["field"] = "url",
            ["value"] = url
        });
    }

    public static McpErrorInfo InvalidSnooze(string message, string? duration, string? snoozeUntilUtc)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidSnooze, message, new Dictionary<string, object?>
        {
            ["duration"] = duration,
            ["snoozeUntilUtc"] = snoozeUntilUtc
        });
    }

    public static McpErrorInfo InvalidSort(string message, string? sort, IReadOnlySet<string> allowedFields)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidSort, message, new Dictionary<string, object?>
        {
            ["sort"] = sort,
            ["allowedFields"] = allowedFields.Order(StringComparer.OrdinalIgnoreCase).ToArray()
        });
    }

    public static McpErrorInfo InvalidStatus(string message, string? status)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidStatus, message, new Dictionary<string, object?>
        {
            ["status"] = status,
            ["allowedStatuses"] = new[] { "open", "fixed", "ignored", "discarded" }
        });
    }

    public static McpErrorInfo InvalidTimeRange(string message, string? last, string? startUtc, string? endUtc)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidTimeRange, message, new Dictionary<string, object?>
        {
            ["last"] = last,
            ["startUtc"] = startUtc,
            ["endUtc"] = endUtc
        });
    }

    public static McpErrorInfo InvalidVersion(string message, string? fixedInVersion)
    {
        return new McpErrorInfo(McpErrorCodes.InvalidVersion, message, new Dictionary<string, object?>
        {
            ["fixedInVersion"] = fixedInVersion
        });
    }

    public static McpErrorInfo NotAccessible(string message, string? resource = null, string? id = null)
    {
        return new McpErrorInfo(McpErrorCodes.NotAccessible, message, ResourceDetails(resource, id));
    }

    public static McpErrorInfo NotFound(string message, string? field = null, string? value = null)
    {
        return new McpErrorInfo(McpErrorCodes.NotFound, message, ResourceDetails(field, value));
    }

    public static McpErrorInfo QueryFailed(string message)
    {
        return new McpErrorInfo(McpErrorCodes.QueryFailed, message);
    }

    public static McpErrorInfo UnknownFilterField(string message, string field, IReadOnlySet<string> allowedFields)
    {
        return new McpErrorInfo(McpErrorCodes.UnknownFilterField, message, new Dictionary<string, object?>
        {
            ["field"] = field,
            ["allowedFields"] = allowedFields.Order(StringComparer.OrdinalIgnoreCase).ToArray()
        });
    }

    private static IReadOnlyDictionary<string, object?>? ResourceDetails(string? field, string? value)
    {
        if (String.IsNullOrEmpty(field) && String.IsNullOrEmpty(value))
            return null;

        return new Dictionary<string, object?>
        {
            ["field"] = field,
            ["value"] = value
        };
    }
}

public sealed record McpErrorInfo(string Code, string Message, IReadOnlyDictionary<string, object?>? Details = null);

public sealed class McpForbiddenException(string message, string requiredScope) : UnauthorizedAccessException(message)
{
    public string RequiredScope { get; } = requiredScope;
}

public sealed record McpResponse<T>(bool Ok, T? Data = default, McpErrorInfo? Error = null, string? Warning = null, McpPagination? Pagination = null)
{
    public static McpResponse<T> Success(T data, string? warning = null, McpPagination? pagination = null)
    {
        return new McpResponse<T>(true, data, Warning: warning, Pagination: pagination);
    }

    public static McpResponse<T> Failed(McpErrorInfo error)
    {
        return new McpResponse<T>(false, Error: error);
    }
}

public sealed record McpListData<T>(IReadOnlyCollection<T> Items);

public sealed record McpPagination(bool HasMore, string? Before, string? After, int Limit);

public sealed record McpTimeRange(DateTime? StartUtc, DateTime? EndUtc)
{
    public bool HasRange => StartUtc.HasValue || EndUtc.HasValue;
}

public sealed record McpFilterFieldsResult(
    McpFilterFieldSet Projects,
    McpFilterFieldSet Stacks,
    McpFilterFieldSet Events);

public sealed record McpFilterFieldSet(
    IReadOnlyCollection<string> FilterFields,
    IReadOnlyCollection<string> SortFields,
    IReadOnlyCollection<string> DynamicFilterPrefixes);

public sealed record McpEventCountResult(
    long Events,
    double Occurrences,
    long Stacks,
    long Users,
    string? Interval,
    DateTime? StartUtc,
    DateTime? EndUtc,
    IReadOnlyCollection<McpEventTrendBucket> Trend,
    string? GroupBy = null,
    IReadOnlyCollection<McpEventCountGroup>? Groups = null);

public sealed record McpEventTrendBucket(
    string Date,
    long Events,
    double Occurrences);

public sealed record McpEventCountGroup(
    string Key,
    long Events,
    double Occurrences,
    IReadOnlyCollection<McpEventTrendBucket> Trend);

public sealed record McpProjectResult(
    string Id,
    string OrganizationId,
    string Name,
    bool? IsConfigured,
    DateTime? LastEventDateUtc,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    string Url);

public sealed record McpStackResult(
    string Id,
    string OrganizationId,
    string ProjectId,
    string Type,
    string Status,
    string Title,
    string? Description,
    int TotalOccurrences,
    DateTime FirstOccurrence,
    DateTime LastOccurrence,
    DateTime? DateFixed,
    string? FixedInVersion,
    DateTime? SnoozeUntilUtc,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<string> References,
    bool OccurrencesAreCritical,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    string Url);

public sealed record McpStackUpdateResult(
    McpStackResult Stack,
    bool Changed,
    string Message);

public sealed record McpEventResult(
    string Id,
    string OrganizationId,
    string ProjectId,
    string StackId,
    string? Type,
    string? Source,
    string? Message,
    DateTimeOffset Date,
    IReadOnlyCollection<string> Tags,
    string? ReferenceId,
    bool IsFirstOccurrence,
    DateTime CreatedUtc,
    string Url,
    McpEventDetails? Details = null);

public sealed record McpEventDetails(
    object? Error,
    RequestInfo? Request,
    EnvironmentInfo? Environment,
    DataDictionary? Data,
    bool IsTruncated = false,
    int? Size = null,
    int? MaxSize = null,
    string? TruncationMessage = null);
