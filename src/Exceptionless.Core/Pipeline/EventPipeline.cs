using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Core.Helpers;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories.Base;
using Foundatio.Logging;
using Foundatio.Metrics;
using Foundatio.Repositories;

namespace Exceptionless.Core.Pipeline {
    public class EventPipeline : PipelineBase<EventContext, EventPipelineActionBase> {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IMetricsClient _metricsClient;

        public EventPipeline(IDependencyResolver dependencyResolver, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IMetricsClient metricsClient, ILoggerFactory loggerFactory = null) : base(dependencyResolver, loggerFactory) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _metricsClient = metricsClient;
        }

        public Task<EventContext> RunAsync(PersistentEvent ev, EventPostInfo epi = null) {
            return RunAsync(new EventContext(ev, epi));
        }

        public Task<ICollection<EventContext>> RunAsync(IEnumerable<PersistentEvent> events, EventPostInfo epi = null) {
            return RunAsync(events.Select(ev => new EventContext(ev, epi)).ToList());
        }

        public override async Task<ICollection<EventContext>> RunAsync(ICollection<EventContext> contexts) {
            if (contexts == null || contexts.Count == 0)
                return contexts ?? new List<EventContext>();

            await _metricsClient.CounterAsync(MetricNames.EventsSubmitted, contexts.Count).AnyContext();
            try {
                if (contexts.Any(c => !String.IsNullOrEmpty(c.Event.Id)))
                    throw new ArgumentException("All Event Ids should not be populated.");

                string projectId = contexts.First().Event.ProjectId;
                if (String.IsNullOrEmpty(projectId))
                    throw new ArgumentException("All Project Ids must be populated.");

                if (contexts.Any(c => c.Event.ProjectId != projectId))
                    throw new ArgumentException("All Project Ids must be the same for a batch of events.");

                var project = await _projectRepository.GetByIdAsync(projectId, o => o.Cache()).AnyContext();
                if (project == null)
                    throw new DocumentNotFoundException(projectId, $"Unable to load project: \"{projectId}\"");

                contexts.ForEach(c => c.Project = project);

                var organization = await _organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache()).AnyContext();
                if (organization == null)
                    throw new DocumentNotFoundException(project.OrganizationId, $"Unable to load organization: \"{project.OrganizationId}\"");

                contexts.ForEach(c => {
                    c.Organization = organization;
                    c.Event.OrganizationId = organization.Id;
                });

                // load organization settings into the context
                foreach (string key in organization.Data.Keys)
                    contexts.ForEach(c => c.SetProperty(key, organization.Data[key]));

                // load project settings into the context, overriding any organization settings with the same name
                foreach (string key in project.Data.Keys)
                    contexts.ForEach(c => c.SetProperty(key, project.Data[key]));

                await _metricsClient.TimeAsync(async () => await base.RunAsync(contexts).AnyContext(), MetricNames.EventsProcessingTime).AnyContext();

                int cancelled = contexts.Count(c => c.IsCancelled);
                if (cancelled > 0)
                    await _metricsClient.CounterAsync(MetricNames.EventsProcessCancelled, cancelled).AnyContext();

                // TODO: Log the errors out to the events project id.
                int errors = contexts.Count(c => c.HasError);
                if (errors > 0)
                    await _metricsClient.CounterAsync(MetricNames.EventsProcessErrors, errors).AnyContext();
            } catch (Exception) {
                await _metricsClient.CounterAsync(MetricNames.EventsProcessErrors, contexts.Count).AnyContext();
                throw;
            }

            return contexts;
        }

        protected override IList<Type> GetActionTypes() {
            return _actionTypeCache.GetOrAdd(typeof(EventPipelineActionBase), t => TypeHelper.GetDerivedTypes<EventPipelineActionBase>(new[] { typeof(EventPipeline).Assembly }).SortByPriority());
        }
    }
}
