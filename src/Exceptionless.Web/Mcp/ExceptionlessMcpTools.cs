using System.ComponentModel;
using System.Security.Claims;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Web.Extensions;
using Foundatio.Repositories;
using Foundatio.Repositories.Extensions;
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

    public ExceptionlessMcpTools(
        IHttpContextAccessor httpContextAccessor,
        IOrganizationRepository organizationRepository,
        IProjectRepository projectRepository,
        IStackRepository stackRepository,
        IEventRepository eventRepository)
    {
        _httpContextAccessor = httpContextAccessor;
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _stackRepository = stackRepository;
        _eventRepository = eventRepository;
    }

    [McpServerTool(Name = "list_projects", ReadOnly = true, UseStructuredContent = true)]
    [Description("Lists projects the authenticated Exceptionless user can access.")]
    public async Task<McpListResult<McpProjectResult>> ListProjectsAsync(
        [Description("Optional Exceptionless filter expression applied to projects.")]
        string? filter = null,
        [Description("Optional sort expression. Defaults to project name.")]
        string? sort = null,
        [Description("Maximum number of projects to return. Defaults to 10 and is capped at 50.")]
        int limit = DefaultLimit)
    {
        try
        {
            EnsureScope(AuthorizationRoles.ProjectsRead);
            var organizations = await GetAccessibleOrganizationsAsync();
            var systemFilter = new AppFilter(organizations)
            {
                IsUserOrganizationsFilter = true
            };

            var results = await _projectRepository.GetByFilterAsync(systemFilter, filter, sort, o => o.PageLimit(ApplyLimit(limit)));

            return new McpListResult<McpProjectResult>(
                results.Documents.Select(ToProjectResult).ToArray(),
                results.HasMore);
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
    [Description("Searches stacks in an Exceptionless project, useful for top issues, top 404s, or recent problem groups.")]
    public async Task<McpListResult<McpStackResult>> SearchStacksAsync(
        [Description("The Exceptionless project id to search within.")]
        string projectId,
        [Description("Optional Exceptionless filter expression. Examples: type:404, status:open, error:true.")]
        string? filter = null,
        [Description("Optional sort expression. Defaults to -last_occurrence.")]
        string? sort = "-last_occurrence",
        [Description("Maximum number of stacks to return. Defaults to 10 and is capped at 50.")]
        int limit = DefaultLimit)
    {
        try
        {
            EnsureScope(AuthorizationRoles.StacksRead);
            var (project, organization) = await GetProjectAndOrganizationAsync(projectId);
            var systemFilter = new AppFilter(project, organization);

            var results = await _stackRepository.FindAsync(
                q => q.AppFilter(systemFilter).FilterExpression(filter).SortExpression(sort ?? "-last_occurrence"),
                o => o.PageLimit(ApplyLimit(limit)));

            return new McpListResult<McpStackResult>(
                results.Documents.Select(ToStackResult).ToArray(),
                results.HasMore);
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
    [Description("Lists recent events in a specific Exceptionless stack.")]
    public async Task<McpListResult<McpEventResult>> GetStackEventsAsync(
        [Description("The Exceptionless stack id.")]
        string stackId,
        [Description("Optional Exceptionless filter expression applied to events.")]
        string? filter = null,
        [Description("Optional sort expression. Defaults to -date.")]
        string? sort = "-date",
        [Description("Maximum number of events to return. Defaults to 10 and is capped at 50.")]
        int limit = DefaultLimit)
    {
        try
        {
            EnsureScope(AuthorizationRoles.EventsRead);
            var (stack, organization) = await GetStackAndOrganizationAsync(stackId);
            var systemFilter = new AppFilter(stack, organization);

            var results = await _eventRepository.FindAsync(
                q => q.AppFilter(systemFilter).FilterExpression(filter).EnforceEventStackFilter().SortExpression(sort ?? "-date"),
                o => o.PageLimit(ApplyLimit(limit)));

            return new McpListResult<McpEventResult>(
                results.Documents.Select(ToEventResult).ToArray(),
                results.HasMore);
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
    [Description("Gets compact details for a specific Exceptionless event.")]
    public async Task<McpItemResult<McpEventResult>> GetEventAsync(
        [Description("The Exceptionless event id.")]
        string eventId)
    {
        try
        {
            EnsureScope(AuthorizationRoles.EventsRead);
            var ev = await _eventRepository.GetByIdAsync(eventId, o => o.Cache());
            if (ev is null)
                return McpItemResult<McpEventResult>.NotFound($"Event {eventId} was not found or is not accessible.");

            EnsureOrganizationAccess(ev.OrganizationId);
            return McpItemResult<McpEventResult>.Success(ToEventResult(ev));
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

    private static int ApplyLimit(int limit)
    {
        return Math.Clamp(limit <= 0 ? DefaultLimit : limit, 1, MaxLimit);
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

    private static McpEventResult ToEventResult(PersistentEvent ev)
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
            $"/api/v2/events/{ev.Id}");
    }

    private static string[] ToTags(IEnumerable<string?>? tags)
    {
        return tags?
            .Where(t => !String.IsNullOrEmpty(t))
            .Select(t => t!)
            .ToArray() ?? [];
    }
}

public sealed record McpListResult<T>(IReadOnlyCollection<T> Items, bool HasMore, string? Error = null)
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
    string Url);
