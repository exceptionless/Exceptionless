using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;

namespace Exceptionless.Core.Plugins.WebHook;

public class WebHookDataContext : ExtensibleObject
{
    public WebHookDataContext(Models.WebHook webHook, PersistentEvent ev, Organization organization = null, Project project = null, Stack stack = null, bool isNew = false, bool isRegression = false)
    {
        WebHook = webHook ?? throw new ArgumentException("WebHook cannot be null.", nameof(webHook));
        Organization = organization;
        Project = project;
        Stack = stack;
        Event = ev ?? throw new ArgumentException("Event cannot be null.", nameof(ev));
        IsNew = isNew;
        IsRegression = isRegression;
    }

    public WebHookDataContext(Models.WebHook webHook, Stack stack, Organization organization = null, Project project = null, bool isNew = false, bool isRegression = false)
    {
        WebHook = webHook ?? throw new ArgumentException("WebHook cannot be null.", nameof(webHook));
        Organization = organization;
        Project = project;
        Stack = stack ?? throw new ArgumentException("Stack cannot be null.", nameof(stack));
        IsNew = isNew;
        IsRegression = isRegression;
    }

    public Models.WebHook WebHook { get; set; }
    public PersistentEvent Event { get; set; }
    public Stack Stack { get; set; }
    public Organization Organization { get; set; }
    public Project Project { get; set; }

    public bool IsNew { get; set; }
    public bool IsRegression { get; set; }

    public bool IsCancelled { get; set; }
}
