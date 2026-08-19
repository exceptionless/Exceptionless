using Exceptionless.Core.Authorization;

namespace Exceptionless.Web.Assistant;

public sealed class AssistantToolContext
{
    public bool ToolsEnabled { get; private set; }
    public string? OrganizationId { get; private set; }

    public IDisposable BeginTools(string? organizationId = null)
    {
        bool wasEnabled = ToolsEnabled;
        string? previousOrganizationId = OrganizationId;
        ToolsEnabled = true;
        OrganizationId = organizationId;
        return new Scope(this, wasEnabled, previousOrganizationId);
    }

    public bool AllowsScope(string scope)
    {
        return ToolsEnabled && (scope is AuthorizationRoles.EventsRead or AuthorizationRoles.ProjectsRead or AuthorizationRoles.StacksRead or AuthorizationRoles.StacksWrite);
    }

    public bool AllowsOrganization(string organizationId)
    {
        return !ToolsEnabled || String.IsNullOrWhiteSpace(OrganizationId) || String.Equals(OrganizationId, organizationId, StringComparison.Ordinal);
    }

    private sealed class Scope(AssistantToolContext context, bool wasEnabled, string? previousOrganizationId) : IDisposable
    {
        public void Dispose()
        {
            context.ToolsEnabled = wasEnabled;
            context.OrganizationId = previousOrganizationId;
        }
    }
}
