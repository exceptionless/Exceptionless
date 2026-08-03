using Exceptionless.Core.Authorization;

namespace Exceptionless.Web.Assistant;

public sealed class AssistantToolContext
{
    public bool ToolsEnabled { get; private set; }

    public IDisposable BeginTools()
    {
        ToolsEnabled = true;
        return new Scope(this);
    }

    public bool AllowsScope(string scope)
    {
        return ToolsEnabled && (scope is AuthorizationRoles.EventsRead or AuthorizationRoles.ProjectsRead or AuthorizationRoles.StacksRead or AuthorizationRoles.StacksWrite);
    }

    private sealed class Scope(AssistantToolContext context) : IDisposable
    {
        public void Dispose()
        {
            context.ToolsEnabled = false;
        }
    }
}
