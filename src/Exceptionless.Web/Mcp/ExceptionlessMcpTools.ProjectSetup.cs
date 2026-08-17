using System.ComponentModel;
using Exceptionless.Core.Authorization;
using ModelContextProtocol.Server;

namespace Exceptionless.Web.Mcp;

public sealed partial class ExceptionlessMcpTools
{
    [McpServerTool(Name = "get_project_setup", Title = "Get project setup", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Gets the canonical Exceptionless Client Setup page and verified client support for a project. Use this for any question about configuring, installing, or connecting an Exceptionless client.")]
    public async Task<McpResponse<McpProjectSetupResult>> GetProjectSetupAsync(
        [Description("Optional Exceptionless project id to configure. May be omitted when a current project is available.")]
        string? projectId = null)
    {
        try
        {
            EnsureScope(AuthorizationRoles.ProjectsRead);
            if (projectId is not null && !TryValidateId(projectId, "projectId", out var idError))
                return McpResponse<McpProjectSetupResult>.Failed(idError);

            var projectContext = await _mcpContextService.ResolveProjectAsync(projectId);
            if (!projectContext.Succeeded)
                return McpResponse<McpProjectSetupResult>.Failed(projectContext.Error!);

            var project = projectContext.Project!;
            return McpResponse<McpProjectSetupResult>.Success(new McpProjectSetupResult(
                project.Id,
                project.Name,
                [
                    new McpProjectSetupClient(".NET", "current"),
                    new McpProjectSetupClient("JavaScript / Node.js", "legacy")
                ],
                ["Use the project's Client Setup page for the server URL, API key, and current installation instructions."]));
        }
        catch (Exception ex) when (IsLookupError(ex))
        {
            return McpResponse<McpProjectSetupResult>.Failed(ToLookupError("Project", projectId ?? "current authorization", ex));
        }
    }
}
