using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Web.Extensions;
using Foundatio.Repositories;
using Foundatio.Repositories.Options;

namespace Exceptionless.Web.Mcp;

public sealed class McpContextService(
    IHttpContextAccessor httpContextAccessor,
    IOrganizationRepository organizationRepository,
    IProjectRepository projectRepository)
{
    private const int CandidateLimit = 100;

    private HttpRequest Request => httpContextAccessor.HttpContext?.Request
        ?? throw new UnauthorizedAccessException("No active request is available.");

    public async Task<McpContextResolution> GetContextAsync(
        string? organizationId = null,
        string? projectId = null,
        bool requireProject = false)
    {
        var accessibleOrganizations = await GetAccessibleOrganizationsAsync();
        if (accessibleOrganizations.Count == 0)
        {
            return McpContextResolution.Failed(McpErrors.NotAccessible("No accessible organizations were found.", "organization"));
        }

        Organization? activeOrganization = null;
        Project? activeProject = null;

        if (projectId is not null)
        {
            var projectAccess = await GetAccessibleProjectAsync(projectId.Trim());
            if (projectAccess.Error is not null)
                return McpContextResolution.Failed(projectAccess.Error);

            activeProject = projectAccess.Project!;
            activeOrganization = accessibleOrganizations.FirstOrDefault(o => String.Equals(o.Id, activeProject.OrganizationId, StringComparison.Ordinal));
            if (activeOrganization is null)
            {
                return McpContextResolution.Failed(McpErrors.NotAccessible(
                    $"Organization {activeProject.OrganizationId} was not found or is not accessible.",
                    "organizationId",
                    activeProject.OrganizationId));
            }

            if (organizationId is not null && !String.Equals(organizationId.Trim(), activeOrganization.Id, StringComparison.Ordinal))
            {
                return McpContextResolution.Failed(McpErrors.ContextMismatch(
                    "The requested project is not in the requested organization.",
                    organizationId.Trim(),
                    activeProject.OrganizationId,
                    activeProject.Id,
                    activeProject.Id));
            }
        }
        else if (organizationId is not null)
        {
            activeOrganization = accessibleOrganizations.FirstOrDefault(o => String.Equals(o.Id, organizationId.Trim(), StringComparison.Ordinal));
            if (activeOrganization is null)
            {
                return McpContextResolution.Failed(McpErrors.NotAccessible(
                    $"Organization {organizationId.Trim()} was not found or is not accessible.",
                    "organizationId",
                    organizationId.Trim()));
            }
        }
        else if (accessibleOrganizations.Count == 1)
        {
            activeOrganization = accessibleOrganizations[0];
        }

        if (activeOrganization is null)
        {
            var context = ToContextResult(null, null, accessibleOrganizations, []);
            return McpContextResolution.Failed(McpErrors.ContextRequired(
                "Specify an organization id before using organization-scoped MCP tools.",
                "organization",
                context.Organizations,
                context.Projects), context);
        }

        var accessibleProjects = await GetOrganizationProjectsAsync(activeOrganization.Id);
        if (activeProject is null && requireProject)
        {
            if (accessibleProjects.Total == 1)
            {
                activeProject = accessibleProjects.Documents.FirstOrDefault();
            }
            else
            {
                var context = ToContextResult(activeOrganization, null, accessibleOrganizations, accessibleProjects.Documents);
                return McpContextResolution.Failed(McpErrors.ContextRequired(
                    "Specify a project id before using this MCP tool.",
                    "project",
                    context.Organizations,
                    context.Projects), context, activeOrganization);
            }
        }

        var result = ToContextResult(activeOrganization, activeProject, accessibleOrganizations, accessibleProjects.Documents);
        return McpContextResolution.Success(result, activeOrganization, activeProject);
    }

    public async Task<McpContextResolution> ListOrganizationsAsync()
    {
        var accessibleOrganizations = await GetAccessibleOrganizationsAsync();
        return McpContextResolution.Success(
            ToContextResult(null, null, accessibleOrganizations, []),
            null,
            null);
    }

    public async Task<McpContextResolution> ResolveProjectByIdOrNameAsync(
        string? projectId = null,
        string? projectName = null,
        string? organizationId = null)
    {
        if (projectId is not null)
            return await GetContextAsync(organizationId, projectId.Trim(), requireProject: true);

        if (String.IsNullOrWhiteSpace(projectName))
        {
            if (organizationId is not null)
                return await GetContextAsync(organizationId: organizationId, requireProject: true);

            var projectContext = await ResolveProjectAsync();
            return projectContext.Succeeded
                ? McpContextResolution.Success(projectContext.Context, projectContext.Organization, projectContext.Project)
                : McpContextResolution.Failed(projectContext.Error!, projectContext.Context);
        }

        var context = await GetContextAsync(organizationId: organizationId, requireProject: false);
        if (!context.Succeeded || context.ActiveOrganization is null)
            return context;

        var projects = await GetOrganizationProjectsAsync(context.ActiveOrganization.Id);
        var matches = projects.Documents
            .Where(p => String.Equals(p.Name, projectName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 0)
            return McpContextResolution.Failed(McpErrors.NotFound($"Project '{projectName}' was not found in the selected organization.", "projectName", projectName));

        if (matches.Length > 1)
        {
            var result = ToContextResult(context.ActiveOrganization, null, context.Context.Organizations.Select(ToOrganization).ToArray(), matches);
            return McpContextResolution.Failed(McpErrors.ContextRequired(
                $"Multiple projects named '{projectName}' were found. Specify a project id.",
                "project",
                result.Organizations,
                result.Projects), result, context.ActiveOrganization);
        }

        return await GetContextAsync(projectId: matches[0].Id, requireProject: true);
    }

    public async Task<McpProjectContextResolution> ResolveProjectAsync(string? projectId = null)
    {
        if (projectId is null)
            return await ResolveOnlyProjectAsync();

        var projectAccess = await GetAccessibleProjectAsync(projectId.Trim());
        if (!projectAccess.Succeeded)
            return McpProjectContextResolution.Failed(projectAccess.Error!);

        var project = projectAccess.Project!;
        var organization = await organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
        if (organization is null)
        {
            return McpProjectContextResolution.Failed(McpErrors.NotAccessible(
                $"Organization {project.OrganizationId} was not found or is not accessible.",
                "organizationId",
                project.OrganizationId));
        }

        var context = ToContextResult(organization, project, [organization], [project]);
        return McpProjectContextResolution.Success(project, organization, context);
    }

    public async Task<McpErrorInfo?> ValidateProjectScopeAsync(string organizationId, string projectId, string? requestedProjectId)
    {
        if (requestedProjectId is not null && String.Equals(projectId, requestedProjectId, StringComparison.Ordinal))
            return null;

        var projectContext = await ResolveProjectAsync(requestedProjectId);
        if (!projectContext.Succeeded)
            return projectContext.Error;

        var requestedOrganization = projectContext.Organization!;
        var requestedProject = projectContext.Project!;
        if (!String.Equals(requestedOrganization.Id, organizationId, StringComparison.Ordinal)
            || !String.Equals(requestedProject.Id, projectId, StringComparison.Ordinal))
        {
            return McpErrors.ContextMismatch(
                "The requested resource does not match the explicitly selected project.",
                requestedOrganization.Id,
                organizationId,
                requestedProject.Id,
                projectId);
        }

        return null;
    }

    private async Task<McpProjectContextResolution> ResolveOnlyProjectAsync()
    {
        var accessibleOrganizations = await GetAccessibleOrganizationsAsync();
        if (accessibleOrganizations.Count == 0)
            return McpProjectContextResolution.Failed(McpErrors.NotAccessible("No accessible organizations were found.", "organization"));

        var projects = await projectRepository.GetByOrganizationIdsAsync(
            accessibleOrganizations.Select(organization => organization.Id).ToArray(),
            o => o.PageLimit(CandidateLimit));

        if (projects.Total != 1)
        {
            var activeOrganization = accessibleOrganizations.Count == 1 ? accessibleOrganizations[0] : null;
            var context = ToContextResult(activeOrganization, null, accessibleOrganizations, projects.Documents);
            return McpProjectContextResolution.Failed(McpErrors.ContextRequired(
                "Specify a project id before using this MCP tool.",
                "project",
                context.Organizations,
                context.Projects), context);
        }

        var project = projects.Documents.Single();
        var projectOrganization = accessibleOrganizations.Single(o => String.Equals(o.Id, project.OrganizationId, StringComparison.Ordinal));
        var resolvedContext = ToContextResult(projectOrganization, project, accessibleOrganizations, [project]);
        return McpProjectContextResolution.Success(project, projectOrganization, resolvedContext);
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

    private Task<Foundatio.Repositories.Models.FindResults<Project>> GetOrganizationProjectsAsync(string organizationId)
    {
        return projectRepository.GetByOrganizationIdAsync(organizationId, o => o.PageLimit(CandidateLimit));
    }

    private static McpContextResult ToContextResult(
        Organization? activeOrganization,
        Project? activeProject,
        IReadOnlyCollection<Organization> organizations,
        IReadOnlyCollection<Project> projects)
    {
        return new McpContextResult(
            activeOrganization?.Id,
            activeOrganization?.Name,
            activeProject?.Id,
            activeProject?.Name,
            organizations.Select(ToOrganizationResult).ToArray(),
            projects.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Id, StringComparer.Ordinal).Select(ToProjectResult).ToArray(),
            activeOrganization is null,
            activeOrganization is not null && activeProject is null && projects.Count > 0);
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
