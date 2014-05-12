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
using CodeSmith.Core.Scheduler;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class RemoveStaleAccountsJob : Job {
        private readonly OrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IUserRepository _userRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IStackRepository _stackRepository;
        private readonly DayStackStatsRepository _dayStackStats;
        private readonly MonthStackStatsRepository _monthStackStats;
        private readonly DayProjectStatsRepository _dayProjectStats;
        private readonly MonthProjectStatsRepository _monthProjectStats;

        public RemoveStaleAccountsJob(OrganizationRepository organizationRepository,
            IProjectRepository projectRepository,
            IUserRepository userRepository,
            IEventRepository eventRepository,
            IStackRepository stackRepository,
            DayStackStatsRepository dayStackStats,
            MonthStackStatsRepository monthStackStats,
            DayProjectStatsRepository dayProjectStats,
            MonthProjectStatsRepository monthProjectStats) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _userRepository = userRepository;
            _eventRepository = eventRepository;
            _stackRepository = stackRepository;
            _dayStackStats = dayStackStats;
            _monthStackStats = monthStackStats;
            _dayProjectStats = dayProjectStats;
            _monthProjectStats = monthProjectStats;
        }

        public override Task<JobResult> RunAsync(JobRunContext context) {
            Log.Info().Message("Remove stale accounts job starting").Write();

            var organizations = _organizationRepository.GetStaleAccounts();
            while (organizations.Count > 0) {
                foreach (var organization in organizations)
                    TryDeleteOrganization(organization);

                organizations = _organizationRepository.GetStaleAccounts();
            }

            return Task.FromResult(new JobResult { Message = "Successfully removed all stale accounts." });
        }

        private void TryDeleteOrganization(Organization organization) {
            try {
                Log.Info().Message("Removing existing empty projects for the organization '{0}' with Id: '{1}'.", organization.Name, organization.Id).Write();
                List<Project> projects = _projectRepository.GetByOrganizationId(organization.Id).ToList();
                if (projects.Any(project => project.TotalEventCount > 0)) {
                    Log.Info().Message("Organization '{0}' with Id: '{1}' has a project with existing data. This organization will not be deleted.", organization.Name, organization.Id).Write();
                    return;
                }

                foreach (Project project in projects) {
                    Log.Info().Message("Resetting all project data for project '{0}' with Id: '{1}'.", project.Name, project.Id).Write();
                    _stackRepository.RemoveAllByProjectId(project.Id);
                    _eventRepository.RemoveAllByProjectId(project.Id);
                    _dayStackStats.RemoveAllByProjectId(project.Id);
                    _monthStackStats.RemoveAllByProjectId(project.Id);
                    _dayProjectStats.RemoveAllByProjectId(project.Id);
                    _monthProjectStats.RemoveAllByProjectId(project.Id);
                }

                Log.Info().Message("Deleting all projects for organization '{0}' with Id: '{1}'.", organization.Name, organization.Id).Write();
                _projectRepository.Remove(projects);

                Log.Info().Message("Removing users from organization '{0}' with Id: '{1}'.", organization.Name, organization.Id).Write();
                List<User> users = _userRepository.GetByOrganizationId(organization.Id).ToList();
                foreach (User user in users) {
                    if (user.OrganizationIds.All(oid => String.Equals(oid, organization.Id))) {
                        Log.Info().Message("Removing user '{0}' as they do not belong to any other organizations.", user.Id, organization.Name, organization.Id).Write();
                        _userRepository.Remove(user.Id);
                    } else {
                        Log.Info().Message("Removing user '{0}' from organization '{1}' with Id: '{2}'", user.Id, organization.Name, organization.Id).Write();
                        user.OrganizationIds.Remove(organization.Id);
                        _userRepository.Save(user);
                    }
                }

                Log.Info().Message("Deleting organization '{0}' with Id: '{1}'.", organization.Name, organization.Id).Write();
                _organizationRepository.Remove(organization);

                // TODO: Send notifications that the organization and projects have been updated.
            } catch (Exception ex) {
                ex.ToExceptionless().MarkAsCritical().AddTags("Remove Stale Accounts").AddObject(organization).Submit();
            }
        }
    }
}