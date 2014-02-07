#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using NLog.Fluent;

namespace Exceptionless.Core.Utility {
    public class ResetDataHelper {
        private readonly ProjectRepository _projectRepository;
        private readonly ErrorRepository _errorRepository;
        private readonly ErrorStackRepository _errorStackRepository;
        private readonly DayStackStatsRepository _dayStackStats;
        private readonly MonthStackStatsRepository _monthStackStats;
        private readonly DayProjectStatsRepository _dayProjectStats;
        private readonly MonthProjectStatsRepository _monthProjectStats;
        private readonly OrganizationRepository _organizationRepository;
        private readonly ErrorStatsHelper _statsHelper;

        public ResetDataHelper(ProjectRepository projectRepository,
            ErrorRepository errorRepository,
            ErrorStackRepository errorStackRepository,
            OrganizationRepository organizationRepository,
            DayStackStatsRepository dayStackStats,
            MonthStackStatsRepository monthStackStats,
            DayProjectStatsRepository dayProjectStats,
            MonthProjectStatsRepository monthProjectStats,
            ErrorStatsHelper errorStatsHelper) {
            _projectRepository = projectRepository;
            _errorRepository = errorRepository;
            _organizationRepository = organizationRepository;
            _errorStackRepository = errorStackRepository;
            _dayStackStats = dayStackStats;
            _monthStackStats = monthStackStats;
            _dayProjectStats = dayProjectStats;
            _monthProjectStats = monthProjectStats;
            _statsHelper = errorStatsHelper;
        }

        public void ResetProjectData(string projectId) {
            if (String.IsNullOrEmpty(projectId))
                return;

            Project project = _projectRepository.GetByIdCached(projectId);
            if (project == null)
                return;

            try {
                _errorStackRepository.RemoveAllByProjectId(projectId);
                _errorRepository.RemoveAllByProjectId(projectId);
                _dayStackStats.RemoveAllByProjectId(projectId);
                _monthStackStats.RemoveAllByProjectId(projectId);
                _dayProjectStats.RemoveAllByProjectId(projectId);
                _monthProjectStats.RemoveAllByProjectId(projectId);

                project.ErrorCount = 0;
                project.StackCount = 0;

                _projectRepository.Update(project);

                IQueryable<Project> orgProjects = _projectRepository.GetByOrganizationId(project.OrganizationId);
                Organization organization = _organizationRepository.GetById(project.OrganizationId);
                organization.ErrorCount = orgProjects.Sum(p => p.ErrorCount);
                organization.StackCount = orgProjects.Sum(p => p.StackCount);
                _organizationRepository.Update(organization);
            } catch (Exception e) {
                Log.Error().Project(projectId).Exception(e).Message("Error resetting project data.").Report().Write();
                throw;
            }
        }

        public void ResetStackData(string errorStackId) {
            if (String.IsNullOrEmpty(errorStackId))
                return;

            ErrorStack stack = _errorStackRepository.GetById(errorStackId);
            if (stack == null)
                return;

            try {
                stack.TotalOccurrences = 0;
                stack.LastOccurrence = DateTime.MinValue.ToUniversalTime();
                stack.FirstOccurrence = DateTime.MinValue.ToUniversalTime();
                _errorStackRepository.Update(stack);

                _statsHelper.DecrementDayProjectStatsByStackId(stack.ProjectId, errorStackId);
                _statsHelper.DecrementMonthProjectStatsByStackId(stack.ProjectId, errorStackId);

                _errorRepository.RemoveAllByErrorStackId(errorStackId);
                _dayStackStats.RemoveAllByErrorStackId(errorStackId);
                _monthStackStats.RemoveAllByErrorStackId(errorStackId);
            } catch (Exception e) {
                Log.Error().Project(stack.ProjectId).Exception(e).Message("Error resetting stack data.").Report().Write();
                throw;
            }
        }
    }
}