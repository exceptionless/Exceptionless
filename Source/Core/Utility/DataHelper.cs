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
using System.Threading.Tasks;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using Exceptionless.Models.Admin;
using NLog.Fluent;

namespace Exceptionless.Core.Utility {
    public class DataHelper {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly IUserRepository _userRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IDayStackStatsRepository _dayStackStats;
        private readonly IMonthStackStatsRepository _monthStackStats;
        private readonly IDayProjectStatsRepository _dayProjectStats;
        private readonly IMonthProjectStatsRepository _monthProjectStats;
        private readonly BillingManager _billingManager;

        public const string SAMPLE_API_KEY = "e3d51ea621464280bbcb79c11fd6483e";

        public DataHelper(IOrganizationRepository organizationRepository,
            IProjectRepository projectRepository,
            IUserRepository userRepository,
            IEventRepository eventRepository,
            IStackRepository stackRepository,
            ITokenRepository tokenRepository,
            IDayStackStatsRepository dayStackStats,
            IMonthStackStatsRepository monthStackStats,
            IDayProjectStatsRepository dayProjectStats,
            IMonthProjectStatsRepository monthProjectStats,
            BillingManager billingManager) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _userRepository = userRepository;
            _eventRepository = eventRepository;
            _stackRepository = stackRepository;
            _tokenRepository = tokenRepository;
            _dayStackStats = dayStackStats;
            _monthStackStats = monthStackStats;
            _dayProjectStats = dayProjectStats;
            _monthProjectStats = monthProjectStats;
            _billingManager = billingManager;
        }

        public async Task ResetProjectDataAsync(string projectId) {
            if (String.IsNullOrEmpty(projectId))
                return;

            Project project = _projectRepository.GetById(projectId);
            if (project == null)
                return;

            try {
                await _stackRepository.RemoveAllByProjectIdAsync(projectId);
                await _eventRepository.RemoveAllByProjectIdAsync(projectId);
                await _dayStackStats.RemoveAllByProjectIdAsync(projectId);
                await _monthStackStats.RemoveAllByProjectIdAsync(projectId);
                await _dayProjectStats.RemoveAllByProjectIdAsync(projectId);
                await _monthProjectStats.RemoveAllByProjectIdAsync(projectId);

                project.EventCount = 0;
                project.StackCount = 0;

                _projectRepository.Save(project);

                var orgProjects = _projectRepository.GetByOrganizationId(project.OrganizationId);
                Organization organization = _organizationRepository.GetById(project.OrganizationId);
                organization.EventCount = orgProjects.Sum(p => p.EventCount);
                organization.StackCount = orgProjects.Sum(p => p.StackCount);
                _organizationRepository.Save(organization);
            } catch (Exception e) {
                Log.Error().Project(projectId).Exception(e).Message("Error resetting project data.").Report().Write();
                throw;
            }
        }

        public async Task ResetStackDataASync(string stackId) {
            if (String.IsNullOrEmpty(stackId))
                return;

            Stack stack = _stackRepository.GetById(stackId);
            if (stack == null)
                return;

            try {
                stack.TotalOccurrences = 0;
                stack.LastOccurrence = DateTime.MinValue.ToUniversalTime();
                stack.FirstOccurrence = DateTime.MinValue.ToUniversalTime();
                _stackRepository.Save(stack);

                _dayProjectStats.DecrementStatsByStackId(stack.ProjectId, stackId);
                _monthProjectStats.DecrementStatsByStackId(stack.ProjectId, stackId);

                await _eventRepository.RemoveAllByStackIdAsync(stackId);
                await _dayStackStats.RemoveAllByStackIdAsync(stackId);
                await _monthStackStats.RemoveAllByStackIdAsync(stackId);
            } catch (Exception e) {
                Log.Error().Project(stack.ProjectId).Exception(e).Message("Error resetting stack data.").Report().Write();
                throw;
            }
        }

        public void CreateSampleOrganizationAndProject(string userId) {
            if (_tokenRepository.GetById(SAMPLE_API_KEY) != null)
                return;

            User user = _userRepository.GetById(userId, true);
            var organization = new Organization { Id = "537650f3b77efe23a47914f3", Name = "Acme" };
            _billingManager.ApplyBillingPlan(organization, BillingManager.UnlimitedPlan, user);
            organization = _organizationRepository.Add(organization);

            var project = new Project { Id = "537650f3b77efe23a47914f4", Name = "Disintegrating Pistol", TimeZone = TimeZone.CurrentTimeZone.StandardName, OrganizationId = organization.Id };
            project.NextSummaryEndOfDayTicks = TimeZoneInfo.ConvertTime(DateTime.Today.AddDays(1), project.DefaultTimeZone()).ToUniversalTime().Ticks;
            project.Configuration.Settings.Add("IncludeConditionalData", "true");
            project.AddDefaultOwnerNotificationSettings(userId);
            project = _projectRepository.Add(project);

            _tokenRepository.Add(new Token {
                Id = SAMPLE_API_KEY,
                OrganizationId = organization.Id,
                UserId = user.Id,
                ExpiresUtc = DateTime.UtcNow.AddYears(100),
                CreatedUtc = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow,
                Type = TokenType.Access,
                Scopes = new HashSet<string>(AuthorizationRoles.GlobalAll),
                DefaultProjectId = project.Id
            });

            _organizationRepository.IncrementStats(project.OrganizationId, projectCount: 1);

            user.OrganizationIds.Add(organization.Id);
            _userRepository.Save(user);
        }
    }
}