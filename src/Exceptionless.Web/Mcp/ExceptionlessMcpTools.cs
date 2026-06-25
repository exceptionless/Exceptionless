using System.ComponentModel;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Web.Extensions;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Extensions;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Exceptionless.Web.Mcp;

[McpServerToolType]
public sealed class ExceptionlessMcpTools
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 50;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly StackQueryValidator _stackQueryValidator;
    private readonly PersistentEventQueryValidator _eventQueryValidator;
    private readonly ITextSerializer _serializer;
    private readonly ILogger<ExceptionlessMcpTools> _logger;

    public ExceptionlessMcpTools(
        IHttpContextAccessor httpContextAccessor,
        IOrganizationRepository organizationRepository,
        IProjectRepository projectRepository,
        IStackRepository stackRepository,
        IEventRepository eventRepository,
        StackQueryValidator stackQueryValidator,
        PersistentEventQueryValidator eventQueryValidator,
        ITextSerializer serializer,
        ILogger<ExceptionlessMcpTools> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _stackRepository = stackRepository;
        _eventRepository = eventRepository;
        _stackQueryValidator = stackQueryValidator;
        _eventQueryValidator = eventQueryValidator;
        _serializer = serializer;
        _logger = logger;
    }

    [McpServerTool(Name = "list_projects", ReadOnly = true, UseStructuredContent = true)]
    [Description("Lists projects the authenticated Exceptionless user can access. When hasMore is true, pass the returned after cursor to fetch the next page or before cursor to fetch the previous page.")]
    public async Task<McpListResult<McpProjectResult>> ListProjectsAsync(
        [Description("Optional Exceptionless filter expression applied to projects.")]
        string? filter = null,
        [Description("Optional sort expression. Defaults to project name.")]
        string? sort = null,
        [Description("Maximum number of projects to return. Defaults to 10 and is capped at 50. Use the returned after or before cursor with the same limit to page through additional results.")]
        int limit = DefaultLimit,
        [Description("Optional cursor returned from a previous response. Fetches results after this cursor.")]
        string? after = null,
        [Description("Optional cursor returned from a previous response. Fetches results before this cursor.")]
        string? before = null)
    {
        try
        {
            EnsureScope(AuthorizationRoles.ProjectsRead);
            if (!TryValidateLimit(limit, out int resolvedLimit, out string? limitError, out string? limitWarning))
                return McpListResult<McpProjectResult>.Failed(limitError ?? "Invalid limit.");

            if (!TryValidateSort(sort, ProjectSortFields, out string? sortError))
                return McpListResult<McpProjectResult>.Failed(sortError ?? "Invalid sort.");

            var organizations = await GetAccessibleOrganizationsAsync();
            var systemFilter = new AppFilter(organizations)
            {
                IsUserOrganizationsFilter = true
            };

            var results = await _projectRepository.GetByFilterAsync(systemFilter, filter, sort, o => o
                .SearchBeforeToken(before, _serializer)
                .SearchAfterToken(after, _serializer)
                .PageLimit(resolvedLimit));

            return new McpListResult<McpProjectResult>(
                results.Documents.Select(ToProjectResult).ToArray(),
                results.HasMore,
                Warning: limitWarning,
                Before: results.Hits.FirstOrDefault()?.GetSortToken(_serializer),
                After: results.HasMore ? results.Hits.LastOrDefault()?.GetSortToken(_serializer) : null,
                Limit: resolvedLimit);
        }
        catch (Exception)
        {
            return McpListResult<McpProjectResult>.Failed("Unable to list projects. Check the filter, sort, and limit values.");
        }
    }

    [McpServerTool(Name = "get_project", ReadOnly = true, UseStructuredContent = true)]
    [Description("Gets summary details for a specific Exceptionless project.")]
    public async Task<McpItemResult<McpProjectResult>> GetProjectAsync(
        [Description("The Exceptionless project id.")]
        string projectId)
    {
        try
        {
            EnsureScope(AuthorizationRoles.ProjectsRead);
            var project = await GetAccessibleProjectAsync(projectId);
            return McpItemResult<McpProjectResult>.Success(ToProjectResult(project));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpItemResult<McpProjectResult>.NotFound($"Project {projectId} was not found or is not accessible.");
        }
    }

    [McpServerTool(Name = "search_stacks", ReadOnly = true, UseStructuredContent = true)]
    [Description("Searches stacks in an Exceptionless project, useful for top issues, top 404s, or recent problem groups. When hasMore is true, pass the returned after cursor to fetch the next page or before cursor to fetch the previous page.")]
    public async Task<McpListResult<McpStackResult>> SearchStacksAsync(
        [Description("The Exceptionless project id to search within.")]
        string projectId,
        [Description("Optional Exceptionless filter expression. Examples: type:404, status:open, error:true.")]
        string? filter = null,
        [Description("Optional sort expression. Defaults to -last_occurrence.")]
        string? sort = "-last_occurrence",
        [Description("Maximum number of stacks to return. Defaults to 10 and is capped at 50. Use the returned after or before cursor with the same limit to page through additional results.")]
        int limit = DefaultLimit,
        [Description("Optional cursor returned from a previous response. Fetches results after this cursor.")]
        string? after = null,
        [Description("Optional cursor returned from a previous response. Fetches results before this cursor.")]
        string? before = null)
    {
        try
        {
            EnsureScope(AuthorizationRoles.StacksRead);
            var validation = await ValidateSearchAsync(filter, sort, limit, StackFilterFields, StackSortFields, _stackQueryValidator);
            if (validation.Error is not null)
                return McpListResult<McpStackResult>.Failed(validation.Error);

            var (project, organization) = await GetProjectAndOrganizationAsync(projectId);
            var systemFilter = new AppFilter(project, organization);

            var results = await _stackRepository.FindAsync(
                q => q.AppFilter(systemFilter).FilterExpression(filter).SortExpression(sort ?? "-last_occurrence"),
                o => o
                    .SearchBeforeToken(before, _serializer)
                    .SearchAfterToken(after, _serializer)
                    .PageLimit(validation.Limit));

            return new McpListResult<McpStackResult>(
                results.Documents.Select(ToStackResult).ToArray(),
                results.HasMore,
                Warning: validation.Warning,
                Before: results.Hits.FirstOrDefault()?.GetSortToken(_serializer),
                After: results.HasMore ? results.Hits.LastOrDefault()?.GetSortToken(_serializer) : null,
                Limit: validation.Limit);
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpListResult<McpStackResult>.Failed($"Project {projectId} was not found or is not accessible.");
        }
        catch (Exception)
        {
            return McpListResult<McpStackResult>.Failed("Unable to search stacks. Check the project id, filter, sort, and limit values.");
        }
    }

    [McpServerTool(Name = "get_stack", ReadOnly = true, UseStructuredContent = true)]
    [Description("Gets summary details for a specific Exceptionless stack.")]
    public async Task<McpItemResult<McpStackResult>> GetStackAsync(
        [Description("The Exceptionless stack id.")]
        string stackId)
    {
        try
        {
            EnsureScope(AuthorizationRoles.StacksRead);
            var stack = await GetAccessibleStackAsync(stackId);
            return McpItemResult<McpStackResult>.Success(ToStackResult(stack));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpItemResult<McpStackResult>.NotFound($"Stack {stackId} was not found or is not accessible.");
        }
    }

    [McpServerTool(Name = "get_stack_events", ReadOnly = true, UseStructuredContent = true)]
    [Description("Lists recent events in a specific Exceptionless stack. When hasMore is true, pass the returned after cursor to fetch the next page or before cursor to fetch the previous page.")]
    public async Task<McpListResult<McpEventResult>> GetStackEventsAsync(
        [Description("The Exceptionless stack id.")]
        string stackId,
        [Description("Optional Exceptionless filter expression applied to events.")]
        string? filter = null,
        [Description("Optional sort expression. Defaults to -date.")]
        string? sort = "-date",
        [Description("Maximum number of events to return. Defaults to 10 and is capped at 50. Use the returned after or before cursor with the same limit to page through additional results.")]
        int limit = DefaultLimit,
        [Description("Optional cursor returned from a previous response. Fetches results after this cursor.")]
        string? after = null,
        [Description("Optional cursor returned from a previous response. Fetches results before this cursor.")]
        string? before = null)
    {
        try
        {
            EnsureScope(AuthorizationRoles.EventsRead);
            var validation = await ValidateSearchAsync(filter, sort, limit, EventFilterFields, EventSortFields, _eventQueryValidator);
            if (validation.Error is not null)
                return McpListResult<McpEventResult>.Failed(validation.Error);

            var (stack, organization) = await GetStackAndOrganizationAsync(stackId);
            var systemFilter = new AppFilter(stack, organization);

            var results = await _eventRepository.FindAsync(
                q => q.AppFilter(systemFilter).FilterExpression(filter).EnforceEventStackFilter().SortExpression(sort ?? "-date"),
                o => o
                    .SearchBeforeToken(before, _serializer)
                    .SearchAfterToken(after, _serializer)
                    .PageLimit(validation.Limit));

            return new McpListResult<McpEventResult>(
                results.Documents.Select(ev => ToEventResult(ev)).ToArray(),
                results.HasMore,
                Warning: validation.Warning,
                Before: results.Hits.FirstOrDefault()?.GetSortToken(_serializer),
                After: results.HasMore ? results.Hits.LastOrDefault()?.GetSortToken(_serializer) : null,
                Limit: validation.Limit);
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpListResult<McpEventResult>.Failed($"Stack {stackId} was not found or is not accessible.");
        }
        catch (Exception)
        {
            return McpListResult<McpEventResult>.Failed("Unable to list stack events. Check the stack id, filter, sort, and limit values.");
        }
    }

    [McpServerTool(Name = "get_event", ReadOnly = true, UseStructuredContent = true)]
    [Description("Gets details for a specific Exceptionless event, including error, request, environment, and extended data when available.")]
    public async Task<McpItemResult<McpEventResult>> GetEventAsync(
        [Description("The Exceptionless event id.")]
        string eventId,
        [Description("Whether to include error, request, environment, and extended data. Defaults to true.")]
        bool includeDetails = true)
    {
        try
        {
            EnsureScope(AuthorizationRoles.EventsRead);
            var ev = await _eventRepository.GetByIdAsync(eventId, o => o.Cache());
            if (ev is null)
                return McpItemResult<McpEventResult>.NotFound($"Event {eventId} was not found or is not accessible.");

            EnsureOrganizationAccess(ev.OrganizationId);
            return McpItemResult<McpEventResult>.Success(ToEventResult(ev, includeDetails));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpItemResult<McpEventResult>.NotFound($"Event {eventId} was not found or is not accessible.");
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

        throw new UnauthorizedAccessException($"Missing required scope {scope}.");
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
            error = $"Limit must be between 1 and {MaxLimit}.";
            return false;
        }

        if (limit > MaxLimit)
        {
            resolvedLimit = MaxLimit;
            warning = $"Limit was capped at {MaxLimit}.";
        }

        error = null;
        return true;
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
            return SearchValidationResult.Failed(limitError ?? "Invalid limit.");

        if (!TryValidateSort(sort, allowedSortFields, out string? sortError))
            return SearchValidationResult.Failed(sortError ?? "Invalid sort.");

        var queryValidation = await queryValidator.ValidateQueryAsync(filter);
        if (!queryValidation.IsValid)
            return SearchValidationResult.Failed($"Invalid filter: {queryValidation.Message ?? "Unable to parse filter."}");

        string? unknownField = GetUnknownFilterField(filter, allowedFilterFields);
        if (unknownField is not null)
            return SearchValidationResult.Failed($"Unknown filter field '{unknownField}'.");

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
            ToTags(stack.Tags),
            stack.References.ToArray(),
            stack.OccurrencesAreCritical,
            stack.CreatedUtc,
            stack.UpdatedUtc,
            $"/api/v2/stacks/{stack.Id}");
    }

    private McpEventResult ToEventResult(PersistentEvent ev, bool includeDetails = false)
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
            includeDetails ? ToEventDetails(ev) : null);
    }

    private McpEventDetails ToEventDetails(PersistentEvent ev)
    {
        return new McpEventDetails(
            ev.GetError(_serializer, _logger) ?? (object?)ev.GetSimpleError(_serializer, _logger),
            ev.GetRequestInfo(_serializer, _logger),
            ev.GetEnvironmentInfo(_serializer, _logger),
            ev.Data);
    }

    private static string[] ToTags(IEnumerable<string?>? tags)
    {
        return tags?
            .Where(t => !String.IsNullOrEmpty(t))
            .Select(t => t!)
            .ToArray() ?? [];
    }

    private static readonly Regex FilterFieldRegex = new(@"(?:^|[\s(])(?<field>@?[A-Za-z_][A-Za-z0-9_@.-]*):", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> ProjectSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "created_utc",
        "updated_utc",
        "last_event_date_utc"
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

    private sealed record SearchValidationResult(int Limit, string? Warning = null, string? Error = null)
    {
        public static SearchValidationResult Failed(string error)
        {
            return new SearchValidationResult(DefaultLimit, Error: error);
        }
    }
}

public sealed record McpListResult<T>(IReadOnlyCollection<T> Items, bool HasMore, string? Error = null, string? Warning = null, string? Before = null, string? After = null, int? Limit = null)
{
    public static McpListResult<T> Failed(string error)
    {
        return new McpListResult<T>([], false, error);
    }
}

public sealed record McpItemResult<T>(bool Found, T? Item, string? Error = null)
{
    public static McpItemResult<T> Success(T item)
    {
        return new McpItemResult<T>(true, item);
    }

    public static McpItemResult<T> NotFound(string error)
    {
        return new McpItemResult<T>(false, default, error);
    }
}

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
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<string> References,
    bool OccurrencesAreCritical,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    string Url);

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
    DataDictionary? Data);
