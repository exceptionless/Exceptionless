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
using Exceptionless.Core.Utility;

namespace Exceptionless.Core.Pipeline {
    [Priority(60)]
    public class UpdateStatsAction : ErrorPipelineActionBase {
        private readonly OrganizationRepository _organizationRepository;
        private readonly ProjectRepository _projectRepository;
        private readonly ErrorStackRepository _errorStackRepository;
        private readonly ErrorStatsHelper _statsHelper;

        public UpdateStatsAction(ErrorStatsHelper statsHelper, OrganizationRepository organizationRepository, ProjectRepository projectRepository, ErrorStackRepository errorStackRepository) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _errorStackRepository = errorStackRepository;
            _statsHelper = statsHelper;
        }

        protected override bool IsCritical { get { return true; } }

        public override void Process(ErrorPipelineContext ctx) {
            _organizationRepository.IncrementStats(ctx.Error.OrganizationId, errorCount: 1, stackCount: ctx.IsNew ? 1 : 0);
            _projectRepository.IncrementStats(ctx.Error.ProjectId, errorCount: 1, stackCount: ctx.IsNew ? 1 : 0);
            if (!ctx.IsNew)
                _errorStackRepository.IncrementStats(ctx.Error.ErrorStackId, ctx.Error.OccurrenceDate.UtcDateTime);

            IEnumerable<TimeSpan> offsets = _projectRepository.GetTargetTimeOffsetsForStats(ctx.Error.ProjectId);
            _statsHelper.Process(ctx.Error, ctx.IsNew, offsets);
        }
    }
}