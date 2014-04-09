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
using Exceptionless.Core.EventPlugins;
using Exceptionless.Core.Utility;

namespace Exceptionless.Core.Pipeline {
    [Priority(60)]
    public class UpdateStatsAction : EventPipelineActionBase {
        private readonly OrganizationRepository _organizationRepository;
        private readonly ProjectRepository _projectRepository;
        private readonly StackRepository _stackRepository;
        private readonly EventStatsHelper _statsHelper;

        public UpdateStatsAction(EventStatsHelper statsHelper, OrganizationRepository organizationRepository, ProjectRepository projectRepository, StackRepository stackRepository) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
            _statsHelper = statsHelper;
        }

        protected override bool IsCritical { get { return true; } }

        public override void Process(EventContext ctx) {
            _organizationRepository.IncrementStats(ctx.Event.OrganizationId, eventCount: 1, stackCount: ctx.IsNew ? 1 : 0);
            _projectRepository.IncrementStats(ctx.Event.ProjectId, eventCount: 1, stackCount: ctx.IsNew ? 1 : 0);
            if (!ctx.IsNew)
                _stackRepository.IncrementStats(ctx.Event.StackId, ctx.Event.Date.UtcDateTime);

            IEnumerable<TimeSpan> offsets = _projectRepository.GetTargetTimeOffsetsForStats(ctx.Event.ProjectId);
            _statsHelper.Process(ctx.Event, ctx.IsNew, offsets);
        }
    }
}