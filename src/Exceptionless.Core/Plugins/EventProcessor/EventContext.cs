﻿using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;

namespace Exceptionless.Core.Plugins.EventProcessor;

public class EventContext : ExtensibleObject, IPipelineContext
{
    public EventContext(PersistentEvent ev, Organization organization, Project project, EventPostInfo? epi = null)
    {
        Organization = organization;
        Project = project;
        Event = ev;
        Event.OrganizationId = organization.Id;
        Event.ProjectId = project.Id;
        IncludePrivateInformation = project.Configuration.Settings.GetBoolean(SettingsDictionary.KnownKeys.IncludePrivateInformation, true);
        EventPostInfo = epi;
        StackSignatureData = new Dictionary<string, string>();
    }

    public PersistentEvent Event { get; set; }
    public EventPostInfo? EventPostInfo { get; set; }
    public Stack? Stack { get; set; }
    public Project Project { get; set; }
    public Organization Organization { get; set; }
    public bool IsDiscarded { get; set; }
    public bool IsNew { get; set; }
    public bool IsRegression { get; set; }
    public bool IncludePrivateInformation { get; set; }
    public string? SignatureHash { get; set; }
    public IDictionary<string, string> StackSignatureData { get; private set; }

    public bool IsCancelled { get; set; }
    public bool IsProcessed { get; set; }

    public bool HasError => ErrorMessage is not null || Exception is not null;

    public void SetError(string message, Exception? ex = null)
    {
        ErrorMessage = message;
        Exception = ex;
    }

    public string? ErrorMessage { get; private set; }
    public Exception? Exception { get; private set; }
}
