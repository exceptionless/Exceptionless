using System.Security.Claims;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Web.Extensions;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Repositories.Options;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;

namespace Exceptionless.Web.Mcp;

public sealed class McpContextService(
    IHttpContextAccessor httpContextAccessor,
    ICacheClient cacheClient,
    IOrganizationRepository organizationRepository,
    IProjectRepository projectRepository,
    IServiceProvider serviceProvider,
    TimeProvider timeProvider)
{
    private const int CandidateLimit = 100;
    private const string CacheKeyPrefix = "mcp:context:";
    private const string SessionHeaderName = "MCP-Session-Id";
    private static readonly TimeSpan ContextLifetime = TimeSpan.FromHours(12);

    private HttpRequest Request => httpContextAccessor.HttpContext?.Request
        ?? throw new UnauthorizedAccessException("No active request is available.");

    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User
        ?? throw new UnauthorizedAccessException("No authenticated user is available.");

    public async Task<McpContextResolution> GetContextAsync(bool requireProject = false)
    {
        string? cacheKey = GetCacheKey();
        if (String.IsNullOrEmpty(cacheKey))
        {
            return McpContextResolution.Failed(McpErrors.ContextRequired(
                "A stable MCP session is required before project context can be stored.",
                "session",
                [],
                []));
        }

        var accessibleOrganizations = await GetAccessibleOrganizationsAsync();
        if (accessibleOrganizations.Count == 0)
        {
            return McpContextResolution.Failed(McpErrors.NotAccessible("No accessible organizations were found.", "organization"));
        }

        var storedContext = await cacheClient.GetAsync<McpStoredContext?>(cacheKey, null);
        if (storedContext is not null)
            await cacheClient.SetExpirationAsync(cacheKey, ContextLifetime);

        bool changed = false;
        string? activeOrganizationId = storedContext?.ActiveOrganizationId;
        string? activeProjectId = storedContext?.ActiveProjectId;

        var accessibleOrganization = accessibleOrganizations.FirstOrDefault(o => String.Equals(o.Id, activeOrganizationId, StringComparison.Ordinal));
        if (!String.IsNullOrEmpty(activeOrganizationId) && accessibleOrganization is null)
        {
            activeOrganizationId = null;
            activeProjectId = null;
            changed = true;
        }

        if (String.IsNullOrEmpty(activeOrganizationId))
        {
            if (accessibleOrganizations.Count != 1)
            {
                var context = ToContextResult(null, null, accessibleOrganizations, []);
                return McpContextResolution.Failed(McpErrors.ContextRequired(
                    "Select an active organization before using project-scoped MCP tools.",
                    "organization",
                    context.Organizations,
                    context.Projects), context);
            }

            accessibleOrganization = accessibleOrganizations[0];
            activeOrganizationId = accessibleOrganization.Id;
            changed = true;
        }

        accessibleOrganization ??= accessibleOrganizations.First(o => String.Equals(o.Id, activeOrganizationId, StringComparison.Ordinal));
        var accessibleProjects = await GetOrganizationProjectsAsync(accessibleOrganization.Id);
        var activeProject = await GetValidatedProjectAsync(activeProjectId, accessibleOrganization.Id);
        if (!String.IsNullOrEmpty(activeProjectId) && activeProject is null)
        {
            activeProjectId = null;
            changed = true;
        }

        if (String.IsNullOrEmpty(activeProjectId) && requireProject)
        {
            if (accessibleProjects.Total == 1)
            {
                activeProject = accessibleProjects.Documents.FirstOrDefault();
                activeProjectId = activeProject?.Id;
                changed = activeProject is not null;
            }
            else
            {
                var context = ToContextResult(accessibleOrganization, null, accessibleOrganizations, accessibleProjects.Documents);
                return McpContextResolution.Failed(McpErrors.ContextRequired(
                    "Select an active project before using this MCP tool.",
                    "project",
                    context.Organizations,
                    context.Projects), context, accessibleOrganization);
            }
        }

        if (changed)
            await SaveContextAsync(cacheKey, activeOrganizationId, activeProjectId);

        var result = ToContextResult(accessibleOrganization, activeProject, accessibleOrganizations, accessibleProjects.Documents, storedContext?.UpdatedUtc);
        return McpContextResolution.Success(result, accessibleOrganization, activeProject);
    }

    public async Task<McpContextResolution> ListOrganizationsAsync()
    {
        var accessibleOrganizations = await GetAccessibleOrganizationsAsync();
        var context = await GetContextAsync(requireProject: false);
        var activeOrganization = context.ActiveOrganization;
        var activeProject = context.ActiveProject;
        var projects = activeOrganization is null
            ? Array.Empty<Project>()
            : (await GetOrganizationProjectsAsync(activeOrganization.Id)).Documents;

        return McpContextResolution.Success(ToContextResult(activeOrganization, activeProject, accessibleOrganizations, projects), activeOrganization, activeProject);
    }

    public async Task<McpContextResolution> SwitchOrganizationAsync(string organizationId)
    {
        string? cacheKey = GetCacheKey();
        if (String.IsNullOrEmpty(cacheKey))
        {
            return McpContextResolution.Failed(McpErrors.ContextRequired(
                "A stable MCP session is required before organization context can be stored.",
                "session",
                [],
                []));
        }

        var accessibleOrganizations = await GetAccessibleOrganizationsAsync();
        var activeOrganization = accessibleOrganizations.FirstOrDefault(o => String.Equals(o.Id, organizationId, StringComparison.Ordinal));
        if (activeOrganization is null)
            return McpContextResolution.Failed(McpErrors.NotAccessible($"Organization {organizationId} was not found or is not accessible.", "organizationId", organizationId));

        var projects = await GetOrganizationProjectsAsync(activeOrganization.Id);
        var activeProject = projects.Total == 1 ? projects.Documents.FirstOrDefault() : null;

        await SaveContextAsync(cacheKey, activeOrganization.Id, activeProject?.Id);
        return McpContextResolution.Success(ToContextResult(activeOrganization, activeProject, accessibleOrganizations, projects.Documents), activeOrganization, activeProject);
    }

    public async Task<McpContextResolution> SwitchProjectAsync(string projectId)
    {
        string? cacheKey = GetCacheKey();
        if (String.IsNullOrEmpty(cacheKey))
        {
            return McpContextResolution.Failed(McpErrors.ContextRequired(
                "A stable MCP session is required before project context can be stored.",
                "session",
                [],
                []));
        }

        var projectAccess = await GetAccessibleProjectAsync(projectId);
        if (projectAccess.Error is not null)
            return McpContextResolution.Failed(projectAccess.Error);

        var project = projectAccess.Project!;
        var accessibleOrganizations = await GetAccessibleOrganizationsAsync();
        var activeOrganization = accessibleOrganizations.FirstOrDefault(o => String.Equals(o.Id, project.OrganizationId, StringComparison.Ordinal));
        if (activeOrganization is null)
            return McpContextResolution.Failed(McpErrors.NotAccessible($"Organization {project.OrganizationId} was not found or is not accessible.", "organizationId", project.OrganizationId));

        var projects = await GetOrganizationProjectsAsync(activeOrganization.Id);
        await SaveContextAsync(cacheKey, activeOrganization.Id, project.Id);
        return McpContextResolution.Success(ToContextResult(activeOrganization, project, accessibleOrganizations, projects.Documents), activeOrganization, project);
    }

    public async Task<McpContextResolution> ResolveProjectContextAsync(string? projectId = null, string? projectName = null, string? organizationId = null)
    {
        if (!String.IsNullOrWhiteSpace(projectId))
            return await SwitchProjectAsync(projectId.Trim());

        if (String.IsNullOrWhiteSpace(projectName))
            return await GetContextAsync(requireProject: true);

        McpContextResolution context;
        if (!String.IsNullOrWhiteSpace(organizationId))
        {
            context = await SwitchOrganizationAsync(organizationId.Trim());
            if (!context.Succeeded)
                return context;
        }
        else
        {
            context = await GetContextAsync(requireProject: false);
            if (!context.Succeeded)
                return context;
        }

        if (context.ActiveOrganization is null)
            return context;

        var projects = await GetOrganizationProjectsAsync(context.ActiveOrganization.Id);
        var matches = projects.Documents
            .Where(p => String.Equals(p.Name, projectName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 0)
            return McpContextResolution.Failed(McpErrors.NotFound($"Project '{projectName}' was not found in the active organization.", "projectName", projectName));

        if (matches.Length > 1)
        {
            var result = ToContextResult(context.ActiveOrganization, context.ActiveProject, context.Context.Organizations.Select(ToOrganization).ToArray(), matches);
            return McpContextResolution.Failed(McpErrors.ContextRequired(
                $"Multiple projects named '{projectName}' were found. Select a project explicitly.",
                "project",
                result.Organizations,
                result.Projects), result, context.ActiveOrganization, context.ActiveProject);
        }

        return await SwitchProjectAsync(matches[0].Id);
    }

    public async Task<McpProjectContextResolution> ResolveProjectAsync(string? projectId)
    {
        if (String.IsNullOrWhiteSpace(projectId))
        {
            var resolvedContext = await GetContextAsync(requireProject: true);
            return resolvedContext.Succeeded && resolvedContext.ActiveProject is not null && resolvedContext.ActiveOrganization is not null
                ? McpProjectContextResolution.Success(resolvedContext.ActiveProject, resolvedContext.ActiveOrganization, resolvedContext.Context)
                : McpProjectContextResolution.Failed(resolvedContext.Error!, resolvedContext.Context);
        }

        var projectAccess = await GetAccessibleProjectAsync(projectId.Trim());
        if (projectAccess.Error is not null)
        {
            return McpProjectContextResolution.Failed(projectAccess.Error);
        }

        var project = projectAccess.Project!;
        var context = await GetContextAsync(requireProject: false);
        if (!context.Succeeded)
            return McpProjectContextResolution.Failed(context.Error!, context.Context);

        if (context.ActiveOrganization is null)
        {
            return McpProjectContextResolution.Failed(McpErrors.ContextRequired(
                "Select an active organization before using project-scoped MCP tools.",
                "organization",
                context.Context.Organizations,
                context.Context.Projects), context.Context);
        }

        if (!String.Equals(project.OrganizationId, context.ActiveOrganization.Id, StringComparison.Ordinal))
        {
            return McpProjectContextResolution.Failed(McpErrors.ContextMismatch(
                "The requested project is not in the active organization.",
                context.ActiveOrganization.Id,
                project.OrganizationId,
                context.ActiveProject?.Id,
                project.Id), context.Context);
        }

        if (context.ActiveProject is not null && !String.Equals(project.Id, context.ActiveProject.Id, StringComparison.Ordinal))
        {
            return McpProjectContextResolution.Failed(McpErrors.ContextMismatch(
                "The requested project does not match the active project.",
                context.ActiveOrganization.Id,
                project.OrganizationId,
                context.ActiveProject.Id,
                project.Id), context.Context);
        }

        if (context.ActiveProject is null)
            context = await SwitchProjectAsync(project.Id);

        return context.Succeeded && context.ActiveOrganization is not null
            ? McpProjectContextResolution.Success(project, context.ActiveOrganization, context.Context)
            : McpProjectContextResolution.Failed(context.Error!, context.Context);
    }

    public async Task<McpErrorInfo?> ValidateProjectScopeAsync(string organizationId, string projectId)
    {
        var context = await GetContextAsync(requireProject: true);
        if (!context.Succeeded)
            return context.Error;

        if (context.ActiveOrganization is null || context.ActiveProject is null)
            return McpErrors.ContextRequired("Select an active project before using this MCP tool.", "project", context.Context.Organizations, context.Context.Projects);

        if (!String.Equals(context.ActiveOrganization.Id, organizationId, StringComparison.Ordinal) || !String.Equals(context.ActiveProject.Id, projectId, StringComparison.Ordinal))
        {
            return McpErrors.ContextMismatch(
                "The requested resource does not match the active MCP context.",
                context.ActiveOrganization.Id,
                organizationId,
                context.ActiveProject.Id,
                projectId);
        }

        return null;
    }

    private async Task<IReadOnlyList<Organization>> GetAccessibleOrganizationsAsync()
    {
        var organizationIds = Request.GetAssociatedOrganizationIds();
        if (organizationIds.Count == 0)
            return [];

        var organizations = await organizationRepository.GetByIdsAsync(organizationIds.Distinct(StringComparer.Ordinal).ToArray(), o => o.Cache());
        return organizations
            .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<McpProjectAccess> GetAccessibleProjectAsync(string projectId)
    {
        var project = await projectRepository.GetByIdAsync(projectId, o => o.Cache());
        if (project is null)
            return McpProjectAccess.Failed(McpErrors.NotFound($"Project {projectId} was not found.", "projectId", projectId));

        if (!Request.GetAssociatedOrganizationIds().Contains(project.OrganizationId))
            return McpProjectAccess.Failed(McpErrors.NotAccessible($"Project {projectId} is not accessible.", "projectId", projectId));

        return McpProjectAccess.Success(project);
    }

    private async Task<Project?> GetValidatedProjectAsync(string? projectId, string organizationId)
    {
        if (String.IsNullOrWhiteSpace(projectId))
            return null;

        var project = await projectRepository.GetByIdAsync(projectId, o => o.Cache());
        if (project is null || !String.Equals(project.OrganizationId, organizationId, StringComparison.Ordinal))
            return null;

        return project;
    }

    private Task<Foundatio.Repositories.Models.FindResults<Project>> GetOrganizationProjectsAsync(string organizationId)
    {
        return projectRepository.GetByOrganizationIdAsync(organizationId, o => o.PageLimit(CandidateLimit));
    }

    private Task SaveContextAsync(string cacheKey, string? activeOrganizationId, string? activeProjectId)
    {
        return cacheClient.SetAsync(cacheKey, new McpStoredContext(activeOrganizationId, activeProjectId, timeProvider.GetUtcNow().UtcDateTime), ContextLifetime);
    }

    private string? GetCacheKey()
    {
        string? sessionId = null;
        if (Request.Headers.TryGetValue(SessionHeaderName, out var sessionHeader))
            sessionId = sessionHeader.FirstOrDefault();

        if (String.IsNullOrWhiteSpace(sessionId))
            sessionId = serviceProvider.GetService<McpSession>()?.SessionId;

        string? userId = User.GetClaimValue(ClaimTypes.NameIdentifier);
        if (String.IsNullOrWhiteSpace(sessionId) || String.IsNullOrWhiteSpace(userId))
            return null;

        string clientId = User.GetClaimValue(IdentityUtils.OAuthClientIdClaim) ?? "user";
        string resource = User.GetClaimValue(IdentityUtils.OAuthResourceClaim) ?? Request.Path.ToString();
        return String.Concat(
            CacheKeyPrefix,
            sessionId.ToSHA1(),
            ":",
            userId.ToSHA1(),
            ":",
            clientId.ToSHA1(),
            ":",
            resource.ToSHA1());
    }

    private static McpContextResult ToContextResult(
        Organization? activeOrganization,
        Project? activeProject,
        IReadOnlyCollection<Organization> organizations,
        IReadOnlyCollection<Project> projects,
        DateTime? updatedUtc = null)
    {
        return new McpContextResult(
            activeOrganization?.Id,
            activeOrganization?.Name,
            activeProject?.Id,
            activeProject?.Name,
            organizations.Select(ToOrganizationResult).ToArray(),
            projects.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Id, StringComparer.Ordinal).Select(ToProjectResult).ToArray(),
            activeOrganization is null,
            activeOrganization is not null && activeProject is null && projects.Count > 1,
            updatedUtc);
    }

    private static Organization ToOrganization(McpOrganizationResult organization)
    {
        return new Organization
        {
            Id = organization.Id,
            Name = organization.Name
        };
    }

    private static McpOrganizationResult ToOrganizationResult(Organization organization)
    {
        return new McpOrganizationResult(
            organization.Id,
            organization.Name,
            $"/api/v2/organizations/{organization.Id}");
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
}

public sealed record McpStoredContext(string? ActiveOrganizationId, string? ActiveProjectId, DateTime UpdatedUtc);

public sealed record McpContextResolution(McpContextResult Context, Organization? ActiveOrganization, Project? ActiveProject, McpErrorInfo? Error)
{
    public bool Succeeded => Error is null;

    public static McpContextResolution Success(McpContextResult context, Organization? activeOrganization, Project? activeProject)
    {
        return new McpContextResolution(context, activeOrganization, activeProject, null);
    }

    public static McpContextResolution Failed(McpErrorInfo error, McpContextResult? context = null, Organization? activeOrganization = null, Project? activeProject = null)
    {
        return new McpContextResolution(context ?? McpContextResult.Empty, activeOrganization, activeProject, error);
    }
}

public sealed record McpProjectAccess(Project? Project, McpErrorInfo? Error)
{
    public bool Succeeded => Error is null;

    public static McpProjectAccess Success(Project project)
    {
        return new McpProjectAccess(project, null);
    }

    public static McpProjectAccess Failed(McpErrorInfo error)
    {
        return new McpProjectAccess(null, error);
    }
}

public sealed record McpProjectContextResolution(Project? Project, Organization? Organization, McpContextResult Context, McpErrorInfo? Error)
{
    public bool Succeeded => Error is null;

    public static McpProjectContextResolution Success(Project project, Organization organization, McpContextResult context)
    {
        return new McpProjectContextResolution(project, organization, context, null);
    }

    public static McpProjectContextResolution Failed(McpErrorInfo error, McpContextResult? context = null)
    {
        return new McpProjectContextResolution(null, null, context ?? McpContextResult.Empty, error);
    }
}
