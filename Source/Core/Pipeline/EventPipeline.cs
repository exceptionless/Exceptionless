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
using CodeSmith.Core.Component;
using CodeSmith.Core.Dependency;
using Exceptionless.Core.EventPlugins;
using Exceptionless.Models;

namespace Exceptionless.Core.Pipeline {
    public class EventPipeline : PipelineBase<EventContext, EventPipelineActionBase> {
        private readonly OrganizationRepository _organizationRepository;
        private readonly ProjectRepository _projectRepository;

        public EventPipeline(IDependencyResolver dependencyResolver, OrganizationRepository organizationRepository, ProjectRepository projectRepository) : base(dependencyResolver) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
        }

        public void Run(Event ev) {
            if (String.IsNullOrEmpty(ev.ProjectId))
                throw new ArgumentException("ProjectId must be populated on the Event.");

            var project = _projectRepository.GetByIdCached(ev.ProjectId);
            if (project == null)
                throw new InvalidOperationException(String.Format("Unable to load project \"{0}\"", ev.ProjectId));

            if (String.IsNullOrEmpty(ev.OrganizationId))
                ev.OrganizationId = project.OrganizationId;

            var ctx = new EventContext(ev) {
                Organization = _organizationRepository.GetByIdCached(ev.OrganizationId),
                Project = project
            };

            if (ctx.Organization == null)
                throw new InvalidOperationException(String.Format("Unable to load organization \"{0}\"", ev.OrganizationId));

            // load organization settings into the context
            foreach (var key in ctx.Organization.Data.Keys)
                ctx.SetProperty(key, ctx.Organization.Data[key]);

            // load project settings into the context, overriding any organization settings with the same name
            foreach (var key in ctx.Project.Data.Keys)
                ctx.SetProperty(key, ctx.Project.Data[key]);

            Run(ctx);
        }

        public void Run(IEnumerable<Event> events) {
            foreach (Event e in events)
                Run(e);
        }
    }
}