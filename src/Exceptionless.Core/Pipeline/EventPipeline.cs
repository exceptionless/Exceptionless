using Exceptionless.Core.Extensions;
using Exceptionless.Core.Helpers;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Queues.Models;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline;

public class EventPipeline : PipelineBase<EventContext, EventPipelineActionBase>
{
    public EventPipeline(IServiceProvider serviceProvider, AppOptions options, ILoggerFactory loggerFactory) : base(serviceProvider, options, loggerFactory) { }

    public Task<EventContext> RunAsync(PersistentEvent ev, Organization organization, Project project, EventPostInfo? epi = null)
    {
        return RunAsync(new EventContext(ev, organization, project, epi));
    }

    public Task<ICollection<EventContext>> RunAsync(IEnumerable<PersistentEvent> events, Organization organization, Project project, EventPostInfo? epi = null)
    {
        return RunAsync(events.Select(ev => new EventContext(ev, organization, project, epi)).ToList());
    }

    public override async Task<ICollection<EventContext>> RunAsync(ICollection<EventContext> contexts)
    {
        if (contexts is null || contexts.Count == 0)
            return contexts ?? new List<EventContext>();

        AppDiagnostics.EventsSubmitted.Add(contexts.Count);
        try
        {
            if (contexts.Any(c => !String.IsNullOrEmpty(c.Event.Id)))
                throw new ArgumentException("All Event Ids should not be populated.");

            var project = contexts.First().Project;
            if (String.IsNullOrEmpty(project.Id))
                throw new ArgumentException("All Project Ids must be populated.");

            if (contexts.Any(c => c.Event.ProjectId != project.Id))
                throw new ArgumentException("All Project Ids must be the same for a batch of events.");

            // load organization settings into the context
            var organization = contexts.First().Organization;
            if (organization.Data is not null)
                foreach (string key in organization.Data.Keys)
                    contexts.ForEach(c => c.SetProperty(key, organization.Data[key]));

            // load project settings into the context, overriding any organization settings with the same name
            if (project.Data is not null)
                foreach (string key in project.Data.Keys)
                    contexts.ForEach(c => c.SetProperty(key, project.Data[key]));

            await AppDiagnostics.EventsProcessingTime.TimeAsync(() => base.RunAsync(contexts)).AnyContext();

            int cancelled = contexts.Count(c => c.IsCancelled);
            if (cancelled > 0)
                AppDiagnostics.EventsProcessCancelled.Add(cancelled);

            int discarded = contexts.Count(c => c.IsDiscarded);
            if (discarded > 0)
                AppDiagnostics.EventsDiscarded.Add(discarded);

            // TODO: Log the errors out to the events project id.
            int errors = contexts.Count(c => c.HasError);
            if (errors > 0)
                AppDiagnostics.EventsProcessErrors.Add(errors);
        }
        catch (Exception)
        {
            AppDiagnostics.EventsProcessErrors.Add(contexts.Count);
            throw;
        }

        return contexts;
    }

    protected override IList<Type> GetActionTypes()
    {
        return _actionTypeCache.GetOrAdd(typeof(EventPipelineActionBase), t => TypeHelper.GetDerivedTypes<EventPipelineActionBase>().SortByPriority());
    }
}
