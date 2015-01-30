#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using Exceptionless.Core.Helpers;

namespace Exceptionless.Core.Pipeline {
    public class EventPipeline : PipelineBase<EventContext, EventPipelineActionBase> {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IAppStatsClient _statsClient;

        public EventPipeline(IDependencyResolver dependencyResolver, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IAppStatsClient statsClient) : base(dependencyResolver) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _statsClient = statsClient;
        }

        public EventContext Run(PersistentEvent ev) {
            return Run(new EventContext(ev));
        }

        public ICollection<EventContext> Run(IEnumerable<PersistentEvent> events) {
            return Run(events.Select(ev => new EventContext(ev)).ToList());
        }

        public override ICollection<EventContext> Run(ICollection<EventContext> contexts) {
            if (contexts == null || contexts.Count == 0)
                return contexts ?? new List<EventContext>();

            _statsClient.Counter(StatNames.EventsSubmitted, contexts.Count);
            try {
                if (contexts.Any(c => !String.IsNullOrEmpty(c.Event.Id)))
                    throw new ArgumentException("All Event Ids should not be populated.");

                string projectId = contexts.First().Event.ProjectId;
                if (String.IsNullOrEmpty(projectId))
                    throw new ArgumentException("All Project Ids must be populated.");

                if (contexts.Any(c => c.Event.ProjectId != projectId))
                    throw new ArgumentException("All Project Ids must be the same for a batch of events.");

                var project = _projectRepository.GetById(projectId, true);
                if (project == null)
                    throw new InvalidOperationException(String.Format("Unable to load project \"{0}\"", projectId));

                contexts.ForEach(c => c.Project = project);

                var organization = _organizationRepository.GetById(project.OrganizationId, true);
                if (organization == null)
                    throw new InvalidOperationException(String.Format("Unable to load organization \"{0}\"", project.OrganizationId));

                contexts.ForEach(c => {
                    c.Organization = organization;
                    c.Event.OrganizationId = organization.Id;
                });

                // load organization settings into the context
                foreach (var key in organization.Data.Keys)
                    contexts.ForEach(c => c.SetProperty(key, organization.Data[key]));

                // load project settings into the context, overriding any organization settings with the same name
                foreach (var key in project.Data.Keys)
                    contexts.ForEach(c => c.SetProperty(key, project.Data[key]));

                _statsClient.Time(() => base.Run(contexts), StatNames.EventsProcessingTime);

                var cancelled = contexts.Count(c => c.IsCancelled);
                if (cancelled > 0)
                    _statsClient.Counter(StatNames.EventsProcessCancelled, cancelled);

                // TODO: Log the errors out to the events proejct id.
                var errors = contexts.Count(c => c.HasError);
                if (errors > 0)
                    _statsClient.Counter(StatNames.EventsProcessErrors, errors);
            } catch (Exception) {
                _statsClient.Counter(StatNames.EventsProcessErrors, contexts.Count);
                throw;
            }

            return contexts;
        }

        protected override IList<Type> GetActionTypes() {
            return _actionTypeCache.GetOrAdd(typeof(EventPipelineActionBase), t => TypeHelper.GetDerivedTypes<EventPipelineActionBase>(new[] { typeof(EventPipeline).Assembly }).SortByPriority());
        }
    }
}