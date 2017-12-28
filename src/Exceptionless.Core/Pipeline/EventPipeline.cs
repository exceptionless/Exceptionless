using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Models;
using Exceptionless.Core.Helpers;
using Exceptionless.Core.Queues.Models;
using Foundatio.Metrics;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline {
    public class EventPipeline : PipelineBase<EventContext, EventPipelineActionBase> {
        public EventPipeline(IServiceProvider serviceProvider, IMetricsClient metricsClient, ILoggerFactory loggerFactory = null) : base(serviceProvider, metricsClient, loggerFactory) {}

        public Task<EventContext> RunAsync(PersistentEvent ev, Organization organization, Project project, EventPostInfo epi = null) {
            return RunAsync(new EventContext(ev, organization, project, epi));
        }

        public Task<ICollection<EventContext>> RunAsync(IEnumerable<PersistentEvent> events, Organization organization, Project project, EventPostInfo epi = null) {
            return RunAsync(events.Select(ev => new EventContext(ev, organization, project, epi)).ToList());
        }

        public override async Task<ICollection<EventContext>> RunAsync(ICollection<EventContext> contexts) {
            if (contexts == null || contexts.Count == 0)
                return contexts ?? new List<EventContext>();

            _metricsClient.Counter(MetricNames.EventsSubmitted, contexts.Count);
            try {
                if (contexts.Any(c => !String.IsNullOrEmpty(c.Event.Id)))
                    throw new ArgumentException("All Event Ids should not be populated.");

                var project = contexts.First().Project;
                if (String.IsNullOrEmpty(project.Id))
                    throw new ArgumentException("All Project Ids must be populated.");

                if (contexts.Any(c => c.Event.ProjectId != project.Id))
                    throw new ArgumentException("All Project Ids must be the same for a batch of events.");

                // load organization settings into the context
                var organization = contexts.First().Organization;
                foreach (string key in organization.Data.Keys)
                    contexts.ForEach(c => c.SetProperty(key, organization.Data[key]));

                // load project settings into the context, overriding any organization settings with the same name
                foreach (string key in project.Data.Keys)
                    contexts.ForEach(c => c.SetProperty(key, project.Data[key]));

                await _metricsClient.TimeAsync(() => base.RunAsync(contexts), MetricNames.EventsProcessingTime).AnyContext();

                int cancelled = contexts.Count(c => c.IsCancelled);
                if (cancelled > 0)
                    _metricsClient.Counter(MetricNames.EventsProcessCancelled, cancelled);

                // TODO: Log the errors out to the events project id.
                int errors = contexts.Count(c => c.HasError);
                if (errors > 0)
                    _metricsClient.Counter(MetricNames.EventsProcessErrors, errors);
            } catch (Exception) {
                _metricsClient.Counter(MetricNames.EventsProcessErrors, contexts.Count);
                throw;
            }

            return contexts;
        }

        protected override IList<Type> GetActionTypes() {
            return _actionTypeCache.GetOrAdd(typeof(EventPipelineActionBase), t => TypeHelper.GetDerivedTypes<EventPipelineActionBase>(new[] { typeof(EventPipeline).Assembly }).SortByPriority());
        }
    }
}
