#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Concurrent;
using CodeSmith.Core.Component;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;

namespace Exceptionless.Core.Pipeline {
    [Priority(60)]
    public class UpdateStatsAction : EventPipelineActionBase {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;

        public UpdateStatsAction(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IStackRepository stackRepository) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
        }

        protected override bool IsCritical { get { return true; } }

        private static readonly ConcurrentDictionary<string, long> _organizationCounters = new ConcurrentDictionary<string, long>();
        private static readonly ConcurrentDictionary<string, long> _projectCounters = new ConcurrentDictionary<string, long>(); 

        public override void Process(EventContext ctx) {
            // TODO: Implement batch incrementing to reduce pipeline cost.
            //_organizationCounters.AddOrUpdate(ctx.Event.OrganizationId, 1, (key, value) => value + 1);
            //_projectCounters.AddOrUpdate(ctx.Event.ProjectId, 1, (key, value) => value + 1);

            _organizationRepository.IncrementEventCounter(ctx.Event.OrganizationId);
            _projectRepository.IncrementEventCounter(ctx.Event.ProjectId);
            if (!ctx.IsNew)
                _stackRepository.IncrementEventCounter(ctx.Event.OrganizationId, ctx.Event.StackId, ctx.Event.Date.UtcDateTime);
        }
    }
}