using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Foundatio.Jobs;
using Foundatio.Lock;
using NLog.Fluent;
#pragma warning disable 1998

namespace Exceptionless.Core.Jobs {
    public class StaleAccountsJob : JobBase {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IUserRepository _userRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IStackRepository _stackRepository;
        private readonly ILockProvider _lockProvider;

        public StaleAccountsJob(OrganizationRepository organizationRepository,
            IProjectRepository projectRepository,
            IUserRepository userRepository,
            IEventRepository eventRepository,
            IStackRepository stackRepository,
            ILockProvider lockProvider)
        {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _userRepository = userRepository;
            _eventRepository = eventRepository;
            _stackRepository = stackRepository;
            _lockProvider = lockProvider;
        }

        protected override IDisposable GetJobLock() {
            return _lockProvider.AcquireLock("StaleAccountsJob");
        }

        protected override async Task<JobResult> RunInternalAsync(CancellationToken token) {
            var organizations = _organizationRepository.GetAbandoned();
            while (organizations.Count > 0 && !token.IsCancellationRequested) {
                foreach (var organization in organizations) {
                    if (token.IsCancellationRequested)
                        return JobResult.Cancelled;

                    TryDeleteOrganization(organization);
                }

                organizations = _organizationRepository.GetAbandoned();
            }

            return JobResult.Success;
        }

        private void TryDeleteOrganization(Organization organization) {
            try {
                Log.Info().Message("Removing empty projects: org=\"{0}\" id={1}", organization.Name, organization.Id).Write();
                List<Project> projects = _projectRepository.GetByOrganizationId(organization.Id).ToList();
                if (projects.Any(project => _eventRepository.GetCountByProjectId(project.Id) > 0)) {
                    Log.Info().Message("Organization has data: org=\"{0}\" id={1}", organization.Name, organization.Id).Write();
                    return;
                }

                Log.Info().Message("Deleting events: org=\"{0}\" id={1}", organization.Name, organization.Id).Write();
                _eventRepository.RemoveAllByProjectIdsAsync(projects.Select(p => p.Id).ToArray()).Wait();

                Log.Info().Message("Deleting stacks: org=\"{0}\" id={1}", organization.Name, organization.Id).Write();
                _stackRepository.RemoveAllByProjectIdsAsync(projects.Select(p => p.Id).ToArray()).Wait();

                Log.Info().Message("Deleting projects: org=\"{0}\" id={1}", organization.Name, organization.Id).Write();
                _projectRepository.Remove(projects);

                Log.Info().Message("Removing users from organization '{0}' with Id: '{1}'.", organization.Name, organization.Id).Write();
                List<User> users = _userRepository.GetByOrganizationId(organization.Id).ToList();
                foreach (User user in users) {
                    if (user.OrganizationIds.All(oid => String.Equals(oid, organization.Id))) {
                        Log.Info().Message("Removing user '{0}' as they do not belong to any other organizations.", user.Id, organization.Name, organization.Id).Write();
                        _userRepository.Remove(user.Id);
                    } else {
                        Log.Info().Message("Removing user '{0}' from organization '{1}' with id: '{2}'", user.Id, organization.Name, organization.Id).Write();
                        user.OrganizationIds.Remove(organization.Id);
                        _userRepository.Save(user);
                    }
                }

                Log.Info().Message("Deleting organization '{0}' with Id: '{1}'.", organization.Name, organization.Id).Write();
                _organizationRepository.Remove(organization);
            } catch (Exception ex) {
                Log.Error().Message("Error removing stale org: org={0} id={1} message=\"{2}\"", organization.Name, organization.Id, ex.Message).Exception(ex).Write();
            }
        }
    }
}