using System.ComponentModel;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Queries.Validation;
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
    private const string EventGroupByDescription = "Optional dimension to group counts by. Supported values: version, type, source, status, tag, stack, user, level, error.type, error.code, os, os.version, browser. Multi-value fields such as tag, error.type, and error.code can place one event into multiple groups, so group totals may sum higher than the overall event total.";
    private const string SnoozeDurationDescription = "Optional relative snooze duration such as 2h, 3d, or 1w. Do not combine with snoozeUntilUtc.";
    private const string ProjectFilterDescription = "Optional Exceptionless filter expression applied to projects. Supported fields: id, name, organization_id, created_utc, updated_utc, last_event_date_utc.";
    private const string StackFilterDescription = "Optional Exceptionless filter expression. Supported fields include: stack, project, project_id, organization, organization_id, type, status, title, description, tag, tags, references, fixed, hidden, regressed, error, first, first_occurrence, last, last_occurrence, occurrences, total_occurrences, data.*, idx.*. data.* only works for fields mapped in the search index; use idx.* for custom indexed data.";
    private const string EventFilterDescription = "Optional Exceptionless filter expression applied to events. Supported fields include: id, project, project_id, stack, stack_id, organization, organization_id, type, source, message, date, tag, tags, user, user.name, user.email, path, error, error.type, error.message, error.code, status, data.*, idx.*. data.* only works for fields mapped in the search index; use idx.* for custom indexed data.";

    private const string IndexedDataFilterNote = "data.* filters only work for fields mapped in the search index. Use idx.* for custom indexed data; arbitrary event detail data is returned by get_event but is not searchable unless it is indexed.";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly McpContextService _mcpContextService;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly ITokenRepository _tokenRepository;
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
        ITokenRepository tokenRepository,
        StackQueryValidator stackQueryValidator,
        PersistentEventQueryValidator eventQueryValidator,
        McpContextService mcpContextService,
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
        _tokenRepository = tokenRepository;
        _stackQueryValidator = stackQueryValidator;
        _eventQueryValidator = eventQueryValidator;
        _semanticVersionParser = semanticVersionParser;
        _serializer = serializer;
        _logger = logger;
        _timeProvider = timeProvider;
        _mcpContextService = mcpContextService;
    }

    [McpServerTool(Name = "get_context", ReadOnly = true, UseStructuredContent = true)]
    [Description("Gets the active MCP organization and project context for this session.")]
    public async Task<McpResponse<McpContextResult>> GetContextAsync()
    {
        try
        {
            EnsureScope(AuthorizationRoles.McpRead);
            var context = await _mcpContextService.GetContextAsync(requireProject: false);
            if (!context.Succeeded)
                return McpResponse<McpContextResult>.Failed(context.Error!);

            return McpResponse<McpContextResult>.Success(context.Context);
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpContextResult>.Failed(ToLookupError("MCP context", "current session", ex));
        }
    }

    [McpServerTool(Name = "list_organizations", ReadOnly = true, UseStructuredContent = true)]
    [Description("Lists organizations available to the current MCP OAuth grant.")]
    public async Task<McpResponse<McpListData<McpOrganizationResult>>> ListOrganizationsAsync()
    {
        try
        {
            EnsureScope(AuthorizationRoles.McpRead);
            var context = await _mcpContextService.ListOrganizationsAsync();
            return McpResponse<McpListData<McpOrganizationResult>>.Success(new McpListData<McpOrganizationResult>(context.Context.Organizations));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpListData<McpOrganizationResult>>.Failed(ToLookupError("Organization", "current user", ex));
        }
    }

    [McpServerTool(Name = "switch_organization", ReadOnly = false, UseStructuredContent = true)]
    [Description("Sets the active MCP organization for this session and clears any active project unless the organization has exactly one project.")]
    public async Task<McpResponse<McpContextResult>> SwitchOrganizationAsync(
        [Description("The Exceptionless organization id to make active.")]
        string organizationId)
    {
        try
        {
            EnsureScope(AuthorizationRoles.McpRead);
            if (!TryValidateId(organizationId, "organizationId", out var idError))
                return McpResponse<McpContextResult>.Failed(idError);

            var context = await _mcpContextService.SwitchOrganizationAsync(organizationId);
            if (!context.Succeeded)
                return McpResponse<McpContextResult>.Failed(context.Error!);

            return McpResponse<McpContextResult>.Success(context.Context);
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpContextResult>.Failed(ToLookupError("Organization", organizationId, ex));
        }
    }

    [McpServerTool(Name = "switch_project", ReadOnly = false, UseStructuredContent = true)]
    [Description("Sets the active MCP project for this session and switches the active organization to the project's organization.")]
    public async Task<McpResponse<McpContextResult>> SwitchProjectAsync(
        [Description("The Exceptionless project id to make active.")]
        string projectId)
    {
        try
        {
            EnsureScope(AuthorizationRoles.McpRead);
            if (!TryValidateId(projectId, "projectId", out var idError))
                return McpResponse<McpContextResult>.Failed(idError);

            var context = await _mcpContextService.SwitchProjectAsync(projectId);
            if (!context.Succeeded)
                return McpResponse<McpContextResult>.Failed(context.Error!);

            return McpResponse<McpContextResult>.Success(context.Context);
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpContextResult>.Failed(ToLookupError("Project", projectId, ex));
        }
    }

    [McpServerTool(Name = "resolve_project_context", ReadOnly = false, UseStructuredContent = true)]
    [Description("Resolves and sets the active MCP project context by project id or exact project name.")]
    public async Task<McpResponse<McpContextResult>> ResolveProjectContextAsync(
        [Description("Optional Exceptionless project id to make active.")]
        string? projectId = null,
        [Description("Optional exact project name to make active within the active organization.")]
        string? projectName = null,
        [Description("Optional organization id to use when resolving a project name.")]
        string? organizationId = null)
    {
        try
        {
            EnsureScope(AuthorizationRoles.McpRead);
            if (!String.IsNullOrWhiteSpace(projectId) && !TryValidateId(projectId, "projectId", out var projectIdError))
                return McpResponse<McpContextResult>.Failed(projectIdError);

            if (!String.IsNullOrWhiteSpace(organizationId) && !TryValidateId(organizationId, "organizationId", out var organizationIdError))
                return McpResponse<McpContextResult>.Failed(organizationIdError);

            var context = await _mcpContextService.ResolveProjectContextAsync(projectId, projectName, organizationId);
            if (!context.Succeeded)
                return McpResponse<McpContextResult>.Failed(context.Error!);

            return McpResponse<McpContextResult>.Success(context.Context);
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpContextResult>.Failed(ToLookupError("Project", projectId ?? projectName ?? "current session", ex));
        }
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

            var context = await _mcpContextService.GetContextAsync(requireProject: false);
            if (!context.Succeeded)
                return McpResponse<McpListData<McpProjectResult>>.Failed(context.Error!);

            var organization = context.ActiveOrganization ?? throw new UnauthorizedAccessException("No active organization is available.");
            var systemFilter = new AppFilter(organization);

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
        [Description("Optional Exceptionless project id. Defaults to the active MCP project context.")]
        string? projectId = null)
    {
        try
        {
            EnsureScope(AuthorizationRoles.ProjectsRead);
            if (!String.IsNullOrWhiteSpace(projectId) && !TryValidateId(projectId, "projectId", out var idError))
                return McpResponse<McpProjectResult>.Failed(idError);

            var projectContext = await _mcpContextService.ResolveProjectAsync(projectId);
            if (!projectContext.Succeeded)
                return McpResponse<McpProjectResult>.Failed(projectContext.Error!);

            return McpResponse<McpProjectResult>.Success(ToProjectResult(projectContext.Project!));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpProjectResult>.Failed(ToLookupError("Project", projectId ?? "active project", ex));
        }
    }

    [McpServerTool(Name = "get_client_setup_instructions", ReadOnly = true, UseStructuredContent = true)]
    [Description("Gets project-specific Exceptionless client setup instructions for sending events from an app. Use this for setup questions such as Expo or React Native apps.")]
    public async Task<McpResponse<McpClientSetupInstructionsResult>> GetClientSetupInstructionsAsync(
        [Description("Optional Exceptionless project id to configure. Defaults to the active MCP project context.")]
        string? projectId = null,
        [Description("Client platform to configure. Supported values: expo, react-native. Use expo for Expo apps.")]
        string platform = "expo")
    {
        try
        {
            EnsureScope(AuthorizationRoles.ProjectsRead);
            if (!String.IsNullOrWhiteSpace(projectId) && !TryValidateId(projectId, "projectId", out var idError))
                return McpResponse<McpClientSetupInstructionsResult>.Failed(idError);

            string normalizedPlatform = platform.Trim().ToLowerInvariant();
            if (!ClientSetupPlatforms.Contains(normalizedPlatform))
                return McpResponse<McpClientSetupInstructionsResult>.Failed(McpErrors.InvalidClientPlatform($"Unsupported client platform '{platform}'.", platform, ClientSetupPlatforms));

            var projectContext = await _mcpContextService.ResolveProjectAsync(projectId);
            if (!projectContext.Succeeded)
                return McpResponse<McpClientSetupInstructionsResult>.Failed(projectContext.Error!);

            var project = projectContext.Project!;
            var tokenResults = await _tokenRepository.GetByTypeAndProjectIdAsync(TokenType.Access, project.Id, o => o.PageLimit(10));
            var token = tokenResults.Documents.FirstOrDefault(t => !t.IsDisabled && !t.IsSuspended);
            string apiKey = token?.Id ?? "YOUR_API_KEY";

            var notes = new List<string>
            {
                "Call Exceptionless.startup once when the app starts, before rendering your root component if possible.",
                "After startup, unhandled JavaScript errors are captured automatically. You can also submit handled exceptions explicitly."
            };

            if (token is null)
                notes.Add("No active project API key was found. Create or enable a project API key in Exceptionless, then replace YOUR_API_KEY.");

            string packageName;
            string documentationUrl;
            List<McpClientSetupStep> steps;

            if (normalizedPlatform == "expo")
            {
                packageName = "@exceptionless/react-native";
                documentationUrl = "https://github.com/exceptionless/Exceptionless.JavaScript/tree/main/packages/react-native";
                notes.Add("Native iOS crash reporting requires an Expo development build or standalone build. JavaScript error reporting works in Expo Go.");
                steps = BuildReactNativeClientSetupSteps(
                    apiKey,
                    "npx expo install @exceptionless/react-native @react-native-async-storage/async-storage",
                    includeExpoPlugin: true);
            }
            else
            {
                packageName = "@exceptionless/react-native";
                documentationUrl = "https://github.com/exceptionless/Exceptionless.JavaScript/tree/main/packages/react-native";
                steps = BuildReactNativeClientSetupSteps(
                    apiKey,
                    "npm install @exceptionless/react-native @react-native-async-storage/async-storage",
                    includeExpoPlugin: false);
            }

            return McpResponse<McpClientSetupInstructionsResult>.Success(new McpClientSetupInstructionsResult(
                project.Id,
                project.Name,
                normalizedPlatform,
                packageName,
                apiKey,
                token is not null,
                documentationUrl,
                steps,
                notes));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpClientSetupInstructionsResult>.Failed(ToLookupError("Project", projectId ?? "active project", ex));
        }
        catch (Exception ex) when (IsExpectedToolError(ex))
        {
            return McpResponse<McpClientSetupInstructionsResult>.Failed(McpErrors.QueryFailed("Unable to build client setup instructions. Check the project id and platform."));
        }
    }

    [McpServerTool(Name = "search_stacks", ReadOnly = true, UseStructuredContent = true)]
    [Description("Searches stacks in an Exceptionless project, useful for top issues, top 404s, or recent problem groups. When pagination.hasMore is true, pass pagination.after to fetch the next page or pagination.before to fetch the previous page.")]
    public async Task<McpResponse<McpListData<McpStackResult>>> SearchStacksAsync(
        [Description("Optional Exceptionless project id to search within. Defaults to the active MCP project context.")]
        string? projectId = null,
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
            if (!String.IsNullOrWhiteSpace(projectId) && !TryValidateId(projectId, "projectId", out var idError))
                return McpResponse<McpListData<McpStackResult>>.Failed(idError);

            var validation = await ValidateSearchAsync(filter, sort, limit, StackFilterFields, StackSortFields, _stackQueryValidator);
            if (validation.Error is not null)
                return McpResponse<McpListData<McpStackResult>>.Failed(validation.Error);

            if (!TryValidatePaginationCursors(after, before, out var cursorError))
                return McpResponse<McpListData<McpStackResult>>.Failed(cursorError);

            if (!TryResolveTimeRange(last, startUtc, endUtc, out var timeRange, out var timeError))
                return McpResponse<McpListData<McpStackResult>>.Failed(timeError);

            var projectContext = await _mcpContextService.ResolveProjectAsync(projectId);
            if (!projectContext.Succeeded)
                return McpResponse<McpListData<McpStackResult>>.Failed(projectContext.Error!);

            var project = projectContext.Project!;
            var organization = projectContext.Organization!;
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
            return McpResponse<McpListData<McpStackResult>>.Failed(ToLookupError("Project", projectId ?? "active project", ex));
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
        [Description("Optional Exceptionless project id to search within. Defaults to the active MCP project context.")]
        string? projectId = null,
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
            if (!String.IsNullOrWhiteSpace(projectId) && !TryValidateId(projectId, "projectId", out var idError))
                return McpResponse<McpListData<McpEventResult>>.Failed(idError);

            var validation = await ValidateSearchAsync(filter, sort, limit, EventFilterFields, EventSortFields, _eventQueryValidator);
            if (validation.Error is not null)
                return McpResponse<McpListData<McpEventResult>>.Failed(validation.Error);

            if (!TryValidatePaginationCursors(after, before, out var cursorError))
                return McpResponse<McpListData<McpEventResult>>.Failed(cursorError);

            if (!TryResolveTimeRange(last, startUtc, endUtc, out var timeRange, out var timeError))
                return McpResponse<McpListData<McpEventResult>>.Failed(timeError);

            var projectContext = await _mcpContextService.ResolveProjectAsync(projectId);
            if (!projectContext.Succeeded)
                return McpResponse<McpListData<McpEventResult>>.Failed(projectContext.Error!);

            var project = projectContext.Project!;
            var organization = projectContext.Organization!;
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
            return McpResponse<McpListData<McpEventResult>>.Failed(ToLookupError("Project", projectId ?? "active project", ex));
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
            var contextError = await _mcpContextService.ValidateProjectScopeAsync(ev.OrganizationId, ev.ProjectId);
            if (contextError is not null)
                return McpResponse<McpEventResult>.Failed(contextError);

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
        [Description("Optional Exceptionless project id to count within. Defaults to the active MCP project context.")]
        string? projectId = null,
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
            if (!String.IsNullOrWhiteSpace(projectId) && !TryValidateId(projectId, "projectId", out var idError))
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

            if (!TryValidateGroupLimit(groupLimit, out int resolvedGroupLimit, out var groupLimitError, out string? groupLimitWarning))
                return McpResponse<McpEventCountResult>.Failed(groupLimitError);

            var projectContext = await _mcpContextService.ResolveProjectAsync(projectId);
            if (!projectContext.Succeeded)
                return McpResponse<McpEventCountResult>.Failed(projectContext.Error!);

            var project = projectContext.Project!;
            var organization = projectContext.Organization!;
            var systemFilter = new AppFilter(project, organization);
            string aggregations = BuildCountEventsAggregations(interval, resolvedGroupBy, resolvedGroupLimit);

            var aggregationValidation = await _eventQueryValidator.ValidateAggregationsAsync(aggregations);
            if (!aggregationValidation.IsValid)
                return McpResponse<McpEventCountResult>.Failed(McpErrors.InvalidGroupBy($"Invalid aggregation: {aggregationValidation.Message ?? "Unable to validate aggregation."}", groupBy, EventGroupByAllowedFields));

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

            string? warning = CombineWarnings(groupLimitWarning, GetGroupByOverlapWarning(resolvedGroupBy));

            return McpResponse<McpEventCountResult>.Success(new McpEventCountResult(
                result.Total,
                GetNumericAggregationValue(result.Aggregations.Sum("sum_count")?.Value, result.Total),
                Convert.ToInt64(result.Aggregations.Cardinality("cardinality_stack_id")?.Value.GetValueOrDefault() ?? 0, CultureInfo.InvariantCulture),
                Convert.ToInt64(result.Aggregations.Cardinality("cardinality_user")?.Value.GetValueOrDefault() ?? 0, CultureInfo.InvariantCulture),
                buckets,
                Interval: interval,
                StartUtc: timeRange.StartUtc,
                EndUtc: timeRange.EndUtc,
                GroupBy: resolvedGroupBy?.Name,
                Groups: groups),
                warning);
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpEventCountResult>.Failed(ToLookupError("Project", projectId ?? "active project", ex));
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

            if (!TryNormalizeReferenceUrl(url, out string referenceLink, out var referenceError))
                return McpResponse<McpStackUpdateResult>.Failed(referenceError);

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
    [Description("Lists supported Exceptionless MCP filter and sort fields for projects, stacks, and events. Dynamic data.* and idx.* filter prefixes are allowed for stacks and events, but data.* only works for fields mapped in the search index; use idx.* for custom indexed data.")]
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

        if (user.HasClaim(ClaimTypes.Role, scope))
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
        var contextError = await _mcpContextService.ValidateProjectScopeAsync(stack.OrganizationId, stack.ProjectId);
        if (contextError is not null)
            throw new McpContextException(contextError);

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
        var contextError = await _mcpContextService.ValidateProjectScopeAsync(stack.OrganizationId, stack.ProjectId);
        if (contextError is not null)
            throw new McpContextException(contextError);

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
            object[]? sortValues = _serializer.Deserialize<object[]>(json);
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

        error = McpErrors.InvalidGroupBy($"Unsupported groupBy field '{groupBy}'.", groupBy, EventGroupByAllowedFields);
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

    private static bool TryNormalizeReferenceUrl(string? url, out string referenceUrl, out McpErrorInfo error)
    {
        referenceUrl = null!;
        if (String.IsNullOrWhiteSpace(url))
        {
            error = McpErrors.InvalidReferenceUrl("url is required.", url);
            return false;
        }

        referenceUrl = url.Trim();
        if (Uri.TryCreate(referenceUrl, UriKind.Absolute, out var uri)
            && uri.IsWellFormedOriginalString()
            && !String.IsNullOrEmpty(uri.Host)
            && (String.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            error = null!;
            return true;
        }

        error = McpErrors.InvalidReferenceUrl("url must be an absolute http or https URL.", url);
        return false;
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
        return ex is ArgumentException or KeyNotFoundException or UnauthorizedAccessException or McpContextException;
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
            McpContextException context => context.Error,
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
            project.CreatedUtc,
            project.UpdatedUtc,
            $"/api/v2/projects/{project.Id}",
            project.IsConfigured,
            project.LastEventDateUtc);
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
            stack.TotalOccurrences,
            stack.FirstOccurrence,
            stack.LastOccurrence,
            ToTags(stack.Tags),
            stack.References.ToArray(),
            stack.OccurrencesAreCritical,
            stack.CreatedUtc,
            stack.UpdatedUtc,
            $"/api/v2/stacks/{stack.Id}",
            stack.Description,
            stack.DateFixed,
            stack.FixedInVersion,
            stack.SnoozeUntilUtc);
    }

    private McpEventResult ToEventResult(PersistentEvent ev, bool includeDetails = false, int maxDetailSize = DefaultMaxDetailSize)
    {
        return new McpEventResult(
            ev.Id,
            ev.OrganizationId,
            ev.ProjectId,
            ev.StackId,
            ev.Date,
            ToTags(ev.Tags),
            ev.IsFirstOccurrence,
            ev.CreatedUtc,
            $"/api/v2/events/{ev.Id}",
            ev.Type,
            ev.Source,
            ev.Message,
            ev.ReferenceId,
            includeDetails ? ToEventDetails(ev, maxDetailSize) : null);
    }

    private McpEventDetails ToEventDetails(PersistentEvent ev, int maxDetailSize)
    {
        var details = new McpEventDetails(
            Error: ev.GetError(_serializer, _logger) ?? (object?)ev.GetSimpleError(_serializer, _logger),
            Request: ev.GetRequestInfo(_serializer, _logger),
            Environment: ev.GetEnvironmentInfo(_serializer, _logger),
            Data: ev.Data);

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
            true,
            originalSize,
            maxDetailSize,
            $"Event detail fields were omitted because event details exceeded maxDetailSize ({maxDetailSize} bytes). Retry with a larger maxDetailSize up to {MaxDetailSize}.");
    }

    private static List<McpClientSetupStep> BuildReactNativeClientSetupSteps(string apiKey, string installCommand, bool includeExpoPlugin)
    {
        var steps = new List<McpClientSetupStep>
        {
            new(
                "Install the client",
                "Install the Exceptionless React Native client and AsyncStorage peer dependency.",
                Command: installCommand,
                Language: "shellscript")
        };

        if (includeExpoPlugin)
        {
            steps.Add(new McpClientSetupStep(
                "Configure Expo",
                "Add the Exceptionless config plugin to app.json when using Expo development or standalone builds.",
                Code: String.Join('\n', new[]
                {
                    "{",
                    "  \"expo\": {",
                    "    \"plugins\": [\"@exceptionless/react-native/expo-plugin\"]",
                    "  }",
                    "}"
                }),
                Language: "json"));
        }

        steps.Add(new McpClientSetupStep(
            "Start Exceptionless",
            "Initialize Exceptionless during app startup.",
            Code: String.Join('\n', new[]
            {
                "import { Exceptionless } from \"@exceptionless/react-native\";",
                String.Empty,
                "await Exceptionless.startup(c => {",
                $"  c.apiKey = \"{apiKey}\";",
                "});"
            }),
            Language: "javascript"));

        steps.Add(new McpClientSetupStep(
            "Send a handled exception",
            "Use this pattern when you catch an exception and still want to send it to Exceptionless.",
            Code: String.Join('\n', new[]
            {
                "try {",
                "  throw new Error(\"Handled React Native exception\");",
                "} catch (error) {",
                "  await Exceptionless.submitException(error);",
                "}"
            }),
            Language: "javascript"));

        return steps;
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
        string? notes = dynamicFilterPrefixes.Contains("data.", StringComparer.OrdinalIgnoreCase) ? IndexedDataFilterNote : null;
        return new McpFilterFieldSet(
            filterFields.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            sortFields.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            dynamicFilterPrefixes,
            notes);
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
                limit,
                results.Hits.FirstOrDefault()?.GetSortToken(_serializer),
                results.HasMore ? results.Hits.LastOrDefault()?.GetSortToken(_serializer) : null));
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

    private static string? CombineWarnings(params string?[] warnings)
    {
        var values = warnings.Where(w => !String.IsNullOrWhiteSpace(w)).ToArray();
        return values.Length == 0 ? null : String.Join(" ", values);
    }

    private static string? GetGroupByOverlapWarning(McpEventGroupBy? groupBy)
    {
        return groupBy?.CanOverlap == true
            ? $"groupBy={groupBy.Name} is multi-value; one event can appear in multiple groups, so group event totals may sum higher than the overall event total."
            : null;
    }

    private static readonly HashSet<string> ClientSetupPlatforms = new(StringComparer.OrdinalIgnoreCase) { "expo", "react-native" };

    private static readonly Regex FilterFieldRegex = new(@"(?:^|[\s(])(?<field>@?[A-Za-z_][A-Za-z0-9_@.-]*):", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex IdRegex = new(@"^[A-Za-z0-9]{24,36}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RelativeTimeRegex = new(@"^(?<value>\d+)(?<unit>[mhdw])$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex IntervalRegex = new(@"^\d+[mhdwM]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] EventGroupByAllowedFields =
    [
        "version",
        "type",
        "source",
        "status",
        "tag",
        "stack",
        "user",
        "level",
        "error.type",
        "error.code",
        "os",
        "os.version",
        "browser"
    ];

    private static readonly IReadOnlyDictionary<string, McpEventGroupBy> EventGroupByFields = new Dictionary<string, McpEventGroupBy>(StringComparer.OrdinalIgnoreCase)
    {
        ["version"] = new("version", EventIndex.Alias.Version),
        ["type"] = new("type", EventIndex.Alias.Type),
        ["source"] = new("source", EventIndex.Alias.Source),
        ["status"] = new("status", "status"),
        ["tag"] = new("tag", "tags", CanOverlap: true),
        ["tags"] = new("tag", "tags", CanOverlap: true),
        ["stack"] = new("stack", EventIndex.Alias.StackId),
        ["stack_id"] = new("stack", "stack_id"),
        ["user"] = new("user", EventIndex.Alias.User),
        ["level"] = new("level", EventIndex.Alias.Level),
        ["error.type"] = new("error.type", EventIndex.Alias.ErrorType, CanOverlap: true),
        ["error.code"] = new("error.code", EventIndex.Alias.ErrorCode, CanOverlap: true),
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

    private sealed record McpEventGroupBy(string Name, string AggregationField, bool CanOverlap = false);
}
