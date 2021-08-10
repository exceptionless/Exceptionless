using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs {
    [Job(Description = "Deletes soft deleted data and enforces data retention.", IsContinuous = false)]
    public class CleanupDataJob : JobWithLockBase, IHealthCheck {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly OrganizationService _organizationService;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly IWebHookRepository _webHookRepository;
        private readonly BillingManager _billingManager;
        private readonly AppOptions _appOptions;
        private readonly ILockProvider _lockProvider;
        private DateTime? _lastRun;

        public CleanupDataJob(
            IOrganizationRepository organizationRepository,
            OrganizationService organizationService,
            IProjectRepository projectRepository,
            IStackRepository stackRepository,
            IEventRepository eventRepository,
            ITokenRepository tokenRepository,
            IWebHookRepository webHookRepository,
            ILockProvider lockProvider,
            BillingManager billingManager,
            AppOptions appOptions,
            ILoggerFactory loggerFactory = null
        ) : base(loggerFactory) {
            _organizationRepository = organizationRepository;
            _organizationService = organizationService;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
            _eventRepository = eventRepository;
            _tokenRepository = tokenRepository;
            _webHookRepository = webHookRepository;
            _billingManager = billingManager;
            _appOptions = appOptions;
            _lockProvider = lockProvider;
        }

        protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default) {
            return _lockProvider.AcquireAsync(nameof(CleanupDataJob), TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            _lastRun = SystemClock.UtcNow;

            await CleanupSoftDeletedOrganizationsAsync(context).AnyContext();
            await CleanupSoftDeletedProjectsAsync(context).AnyContext();
            await CleanupSoftDeletedStacksAsync(context).AnyContext();

            await EnforceRetentionAsync(context).AnyContext();

            _logger.CleanupFinished();

            return JobResult.Success;
        }

        private async Task CleanupSoftDeletedOrganizationsAsync(JobContext context) {
            var organizationResults = await _organizationRepository.GetAllAsync(o => o.SoftDeleteMode(SoftDeleteQueryMode.DeletedOnly).SearchAfterPaging().PageLimit(5)).AnyContext();
            _logger.CleanupOrganizationSoftDeletes(organizationResults.Total);

            while (organizationResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var organization in organizationResults.Documents) {
                    using var _ = _logger.BeginScope(new ExceptionlessState().Organization(organization.Id));
                    await RemoveOrganizationAsync(organization, context).AnyContext();

                    // Sleep so we are not hammering the backend.
                    await SystemClock.SleepAsync(TimeSpan.FromSeconds(5)).AnyContext();
                }

                if (context.CancellationToken.IsCancellationRequested || !await organizationResults.NextPageAsync().AnyContext())
                    break;
            }
        }

        private async Task CleanupSoftDeletedProjectsAsync(JobContext context) {
            var projectResults = await _projectRepository.GetAllAsync(o => o.SoftDeleteMode(SoftDeleteQueryMode.DeletedOnly).SearchAfterPaging().PageLimit(5)).AnyContext();
            _logger.CleanupProjectSoftDeletes(projectResults.Total);

            while (projectResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var project in projectResults.Documents) {
                    using var _ = _logger.BeginScope(new ExceptionlessState().Organization(project.OrganizationId).Project(project.Id));
                    await RemoveProjectsAsync(project, context).AnyContext();

                    // Sleep so we are not hammering the backend.
                    await SystemClock.SleepAsync(TimeSpan.FromSeconds(5)).AnyContext();
                }

                if (context.CancellationToken.IsCancellationRequested || !await projectResults.NextPageAsync().AnyContext())
                    break;
            }
        }

        private async Task CleanupSoftDeletedStacksAsync(JobContext context) {
            var stackResults = await _stackRepository.GetSoftDeleted().AnyContext();
            _logger.CleanupStackSoftDeletes(stackResults.Total);

            while (stackResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                await RemoveStacksAsync(stackResults.Documents, context).AnyContext();

                if (context.CancellationToken.IsCancellationRequested || !await stackResults.NextPageAsync().AnyContext())
                    break;
            }
        }

        private async Task RemoveOrganizationAsync(Organization organization, JobContext context) {
            _logger.RemoveOrganizationStart(organization.Name, organization.Id);
            await _organizationService.RemoveTokensAsync(organization).AnyContext();
            await _organizationService.RemoveWebHooksAsync(organization).AnyContext();
            await _organizationService.CancelSubscriptionsAsync(organization).AnyContext();
            await _organizationService.RemoveUsersAsync(organization, null).AnyContext();

            await RenewLockAsync(context).AnyContext();
            long removedEvents = await _eventRepository.RemoveAllByOrganizationIdAsync(organization.Id).AnyContext();

            await RenewLockAsync(context).AnyContext();
            long removedStacks = await _stackRepository.RemoveAllByOrganizationIdAsync(organization.Id).AnyContext();

            await RenewLockAsync(context).AnyContext();
            long removedProjects = await _projectRepository.RemoveAllByOrganizationIdAsync(organization.Id).AnyContext();

            await _organizationRepository.RemoveAsync(organization).AnyContext();
            _logger.RemoveOrganizationComplete(organization.Name, organization.Id, removedProjects, removedStacks, removedEvents);
        }

        private async Task RemoveProjectsAsync(Project project, JobContext context) {
            _logger.RemoveProjectStart(project.Name, project.Id);
            await _tokenRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();
            await _webHookRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();

            await RenewLockAsync(context).AnyContext();
            long removedEvents = await _eventRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();

            await RenewLockAsync(context).AnyContext();
            long removedStacks = await _stackRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();

            await _projectRepository.RemoveAsync(project).AnyContext();
            _logger.RemoveProjectComplete(project.Name, project.Id, removedStacks, removedEvents);
        }

        private async Task RemoveStacksAsync(IReadOnlyCollection<Stack> stacks, JobContext context) {
            foreach (var group in stacks.GroupBy(s => s.ProjectId)) {
                var groupedStacks = group.ToList();
                string organizationId = groupedStacks.Single().OrganizationId;
                string projectId = groupedStacks.Single().ProjectId;
                string[] stackIds = groupedStacks.Select(s => s.Id).ToArray();

                await RenewLockAsync(context).AnyContext();
                using var _ = _logger.BeginScope(new ExceptionlessState().Organization(organizationId).Project(projectId));

                long removedEvents = await _eventRepository.RemoveAllByStackIdsAsync(organizationId, projectId, stackIds).AnyContext();
                await _stackRepository.RemoveAsync(groupedStacks).AnyContext();
                _logger.RemoveStacksComplete(stackIds, removedEvents);
            }
        }
        
        private async Task EnforceRetentionAsync(JobContext context) {
            var results = await _organizationRepository.GetByRetentionDaysEnabledAsync(o => o.SearchAfterPaging().PageLimit(100)).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var organization in results.Documents) {
                    using var _ = _logger.BeginScope(new ExceptionlessState().Organization(organization.Id));
                    int retentionDays = organization.RetentionDays;
                    if (_appOptions.MaximumRetentionDays > 0 && retentionDays > _appOptions.MaximumRetentionDays)
                        retentionDays = _appOptions.MaximumRetentionDays;

                    if (retentionDays < 1)
                        continue;

                    var nextPlan = _billingManager.GetBillingPlanByUpsellingRetentionPeriod(organization.RetentionDays);
                    if (nextPlan != null)
                        retentionDays = nextPlan.RetentionDays;

                    await EnforceStackRetentionDaysAsync(organization, retentionDays + 60, context).AnyContext();
                    await EnforceEventRetentionDaysAsync(organization, retentionDays, context).AnyContext();

                    // Sleep so we are not hammering the backend.
                    await SystemClock.SleepAsync(TimeSpan.FromSeconds(5)).AnyContext();
                }

                if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync().AnyContext())
                    break;
            }
        }

        private async Task EnforceStackRetentionDaysAsync(Organization organization, int retentionDays, JobContext context) {
            await RenewLockAsync(context).AnyContext();

            var cutoff = SystemClock.UtcNow.Date.SubtractDays(retentionDays);
            var stackResults = await _stackRepository.GetStacksForCleanupAsync(organization.Id, cutoff).AnyContext();
            _logger.RetentionEnforcementStackStart(cutoff, organization.Name, organization.Id, stackResults.Total);
            
            while (stackResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                await RemoveStacksAsync(stackResults.Documents, context).AnyContext();

                if (context.CancellationToken.IsCancellationRequested || !await stackResults.NextPageAsync().AnyContext())
                    break;
            }
            
            _logger.RetentionEnforcementStackComplete(organization.Name, organization.Id, stackResults.Documents.Count);
        }

        private async Task EnforceEventRetentionDaysAsync(Organization organization, int retentionDays, JobContext context) {
            await RenewLockAsync(context).AnyContext();

            var cutoff = SystemClock.UtcNow.Date.SubtractDays(retentionDays);
            _logger.RetentionEnforcementEventStart(cutoff, organization.Name, organization.Id);

            long removedEvents = await _eventRepository.RemoveAllAsync(organization.Id, null, null, cutoff).AnyContext();
            _logger.RetentionEnforcementEventComplete(organization.Name, organization.Id, removedEvents);
        }

        private Task RenewLockAsync(JobContext context) {
            _lastRun = SystemClock.UtcNow;
            return context.RenewLockAsync();
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            if (!_lastRun.HasValue)
                return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));

            if (SystemClock.UtcNow.Subtract(_lastRun.Value) > TimeSpan.FromMinutes(65))
                return Task.FromResult(HealthCheckResult.Unhealthy("Job has not run in the last 65 minutes."));

            return Task.FromResult(HealthCheckResult.Healthy("Job has run in the last 65 minutes."));
        }
    }
}