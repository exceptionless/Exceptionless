using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;

namespace Exceptionless.Core.Plugins.WebHook;

public class WebHookDataContext : ExtensibleObject
{
    public WebHookDataContext(Models.WebHook webHook, Organization organization, Project project, Stack stack, PersistentEvent? ev, bool isNew = false, bool isRegression = false)
    {
        WebHook = webHook;
        Organization = organization;
        Project = project;
        Stack = stack;
        Event = ev;
        IsNew = isNew;
        IsRegression = isRegression;
    }

    public Models.WebHook WebHook { get; init; }
    public Organization Organization { get; init; }
    public Project Project { get; init; }
    public Stack Stack { get; init; }
    public PersistentEvent? Event { get; init; }

    public bool IsNew { get; init; }
    public bool IsRegression { get; init; }
    public bool IsCancelled { get; set; }
}
