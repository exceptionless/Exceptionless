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
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using NLog.Fluent;

namespace Exceptionless.Core.Utility {
    public class DataHelper {
        private readonly OrganizationRepository _organizationRepository;
        private readonly ProjectRepository _projectRepository;
        private readonly UserRepository _userRepository;
        private readonly EventRepository _eventRepository;
        private readonly StackRepository _stackRepository;
        private readonly DayStackStatsRepository _dayStackStats;
        private readonly MonthStackStatsRepository _monthStackStats;
        private readonly DayProjectStatsRepository _dayProjectStats;
        private readonly MonthProjectStatsRepository _monthProjectStats;
        private readonly EventStatsHelper _statsHelper;
        private readonly BillingManager _billingManager;

        public const string SAMPLE_API_KEY = "e3d51ea621464280bbcb79c11fd6483e";

        public DataHelper(OrganizationRepository organizationRepository,
            ProjectRepository projectRepository,
            UserRepository userRepository,
            EventRepository eventRepository,
            StackRepository stackRepository,
            DayStackStatsRepository dayStackStats,
            MonthStackStatsRepository monthStackStats,
            DayProjectStatsRepository dayProjectStats,
            MonthProjectStatsRepository monthProjectStats,
            EventStatsHelper eventStatsHelper,
            BillingManager billingManager) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _userRepository = userRepository;
            _eventRepository = eventRepository;
            _stackRepository = stackRepository;
            _dayStackStats = dayStackStats;
            _monthStackStats = monthStackStats;
            _dayProjectStats = dayProjectStats;
            _monthProjectStats = monthProjectStats;
            _statsHelper = eventStatsHelper;
            _billingManager = billingManager;
        }

        public void ResetProjectData(string projectId) {
            if (String.IsNullOrEmpty(projectId))
                return;

            Project project = _projectRepository.GetByIdCached(projectId);
            if (project == null)
                return;

            try {
                _stackRepository.RemoveAllByProjectId(projectId);
                _eventRepository.RemoveAllByProjectId(projectId);
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

            Stack stack = _stackRepository.GetById(errorStackId);
            if (stack == null)
                return;

            try {
                stack.TotalOccurrences = 0;
                stack.LastOccurrence = DateTime.MinValue.ToUniversalTime();
                stack.FirstOccurrence = DateTime.MinValue.ToUniversalTime();
                _stackRepository.Update(stack);

                _statsHelper.DecrementDayProjectStatsByStackId(stack.ProjectId, errorStackId);
                _statsHelper.DecrementMonthProjectStatsByStackId(stack.ProjectId, errorStackId);

                _eventRepository.RemoveAllByStackId(errorStackId);
                _dayStackStats.RemoveAllByErrorStackId(errorStackId);
                _monthStackStats.RemoveAllByErrorStackId(errorStackId);
            } catch (Exception e) {
                Log.Error().Project(stack.ProjectId).Exception(e).Message("Error resetting stack data.").Report().Write();
                throw;
            }
        }

        public void CreateSampleOrganizationAndProject(string userId) {
            if (_projectRepository.GetByApiKey(SAMPLE_API_KEY) != null)
                return;

            User user = _userRepository.GetByIdCached(userId);
            var organization = new Organization { Name = "Acme" };
            _billingManager.ApplyBillingPlan(organization, BillingManager.UnlimitedPlan, user);
            organization = _organizationRepository.Add(organization);

            var project = new Project { Name = "Disintegrating Pistol", TimeZone = TimeZone.CurrentTimeZone.StandardName, OrganizationId = organization.Id };
            project.NextSummaryEndOfDayTicks = TimeZoneInfo.ConvertTime(DateTime.Today.AddDays(1), project.DefaultTimeZone()).ToUniversalTime().Ticks;
            project.ApiKeys.Add(SAMPLE_API_KEY);
            project.Configuration.Settings.Add("IncludeConditionalData", "true");
            project.AddDefaultOwnerNotificationSettings(userId);
            project = _projectRepository.Add(project);

            _organizationRepository.IncrementStats(project.OrganizationId, projectCount: 1);

            user.OrganizationIds.Add(organization.Id);
            _userRepository.Update(user);
        }
    }
}