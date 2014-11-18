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
using CodeSmith.Core.Component;
using CodeSmith.Core.Dependency;
using CodeSmith.Core.Extensions;
using CodeSmith.Core.Helpers;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

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

        public void Run(PersistentEvent ev) {
            Run(new EventContext(ev));
        }

        protected override void Run(EventContext context, IEnumerable<Type> actionTypes) {
            _statsClient.Counter(StatNames.EventsSubmitted);
            try {
                if (!String.IsNullOrEmpty(context.Event.Id))
                    throw new ArgumentException("Event Id should not be populated.");

                if (String.IsNullOrEmpty(context.Event.ProjectId))
                    throw new ArgumentException("ProjectId must be populated on the Event.");

                if (context.Project == null)
                    context.Project = _projectRepository.GetById(context.Event.ProjectId, true);

                if (context.Project == null)
                    throw new InvalidOperationException(String.Format("Unable to load project \"{0}\"", context.Event.ProjectId));

                if (String.IsNullOrEmpty(context.Event.OrganizationId))
                    context.Event.OrganizationId = context.Project.OrganizationId;

                if (context.Organization == null)
                    context.Organization = _organizationRepository.GetById(context.Event.OrganizationId, true);

                if (context.Organization == null)
                    throw new InvalidOperationException(String.Format("Unable to load organization \"{0}\"", context.Event.OrganizationId));

                // load organization settings into the context
                foreach (var key in context.Organization.Data.Keys)
                    context.SetProperty(key, context.Organization.Data[key]);

                // load project settings into the context, overriding any organization settings with the same name
                foreach (var key in context.Project.Data.Keys)
                    context.SetProperty(key, context.Project.Data[key]);

                _statsClient.Time(() => base.Run(context, actionTypes), StatNames.EventsProcessingTime);
                if (context.IsCancelled)
                    _statsClient.Counter(StatNames.EventsProcessCancelled);
            } catch (Exception) {
                _statsClient.Counter(StatNames.EventsProcessErrors);
                throw;
            }
        }

        protected override IList<Type> GetActionTypes() {
            return _actionTypeCache.GetOrAdd(typeof(EventPipelineActionBase), t => TypeHelper.GetDerivedTypes<EventPipelineActionBase>(new[] { typeof(EventPipeline).Assembly }).SortByPriority());
        }
    }
}