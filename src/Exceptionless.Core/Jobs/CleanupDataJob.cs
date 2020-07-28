using System;
using System.Threading;
using System.Threading.Tasks;
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
    [Job(Description = "Deletes soft deleted data and retention data.", InitialDelay = "15m", Interval = "1h")]
    public class CleanupDataJob : JobWithLockBase, IHealthCheck {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly OrganizationService _organizationService;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly IWebHookRepository _webHookRepository;
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
            ICacheClient cacheClient, 
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
            _appOptions = appOptions;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromDays(1));
        }

        protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default) {
            return _lockProvider.AcquireAsync(nameof(StackEventCountJob), TimeSpan.FromHours(2), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            _lastRun = SystemClock.UtcNow;

            await CleanupSoftDeletedOrganizationsAsync(context).AnyContext();
            await CleanupSoftDeletedProjectsAsync(context).AnyContext();
            await CleanupSoftDeletedStacksAsync(context).AnyContext();
            await EnforceEventRetentionAsync(context).AnyContext();
            
            return JobResult.Success;
        }
        
        private async Task CleanupSoftDeletedOrganizationsAsync(JobContext context) {
            var organizationResults = await _organizationRepository.GetAllAsync(q => q.SoftDeleteMode(SoftDeleteQueryMode.DeletedOnly).SnapshotPaging().PageLimit(5)).AnyContext();
            _logger.LogInformation("Cleaning up {OrganizationTotal} soft deleted organization(s)", organizationResults.Total);

            while (organizationResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var organization in organizationResults.Documents) {
                    using (_logger.BeginScope(new ExceptionlessState().Organization(organization.Id))) {
                        await RemoveOrganizationAsync(organization, context).AnyContext();

                        // Sleep so we are not hammering the backend.
                        await SystemClock.SleepAsync(TimeSpan.FromSeconds(5)).AnyContext();
                    }
                }

                if (context.CancellationToken.IsCancellationRequested || !await organizationResults.NextPageAsync().AnyContext())
                    break;
            }
        }
        
        private async Task CleanupSoftDeletedProjectsAsync(JobContext context) {
            var projectResults = await _projectRepository.GetAllAsync(q => q.SoftDeleteMode(SoftDeleteQueryMode.DeletedOnly).SnapshotPaging().PageLimit(5)).AnyContext();
            _logger.LogInformation("Cleaning up {ProjectTotal} soft deleted project(s)", projectResults.Total);

            while (projectResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var project in projectResults.Documents) {
                    using (_logger.BeginScope(new ExceptionlessState().Organization(project.OrganizationId).Project(project.Id))) {
                        await RemoveProjectsAsync(project, context).AnyContext();

                        // Sleep so we are not hammering the backend.
                        await SystemClock.SleepAsync(TimeSpan.FromSeconds(5)).AnyContext();
                    }
                }

                if (context.CancellationToken.IsCancellationRequested || !await projectResults.NextPageAsync().AnyContext())
                    break;
            }
        }
        
        private async Task CleanupSoftDeletedStacksAsync(JobContext context)
        {
            var stackResults = await _stackRepository.GetAllAsync(q => q.SoftDeleteMode(SoftDeleteQueryMode.DeletedOnly).SnapshotPaging().PageLimit(100)).AnyContext();
            _logger.LogInformation("Cleaning up {StackTotal} soft deleted stack(s)", stackResults.Total);

            while (stackResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var stack in stackResults.Documents) {
                    using (_logger.BeginScope(new ExceptionlessState().Organization(stack.OrganizationId).Project(stack.ProjectId))) {
                        await RemoveStackAsync(stack, context).AnyContext();

                        // Sleep so we are not hammering the backend.
                        await SystemClock.SleepAsync(TimeSpan.FromSeconds(5)).AnyContext();
                    }
                }

                if (context.CancellationToken.IsCancellationRequested || !await stackResults.NextPageAsync().AnyContext())
                    break;
            }
        }

        private async Task RemoveOrganizationAsync(Organization organization, JobContext context) {
            _logger.LogInformation("Removing organization: {Organization} ({OrganizationId})", organization.Name, organization.Id);
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
            _logger.LogInformation("Removed organization: {Organization} ({OrganizationId}), Removed {RemovedProjects} Projects, {RemovedStacks} Stacks, {RemovedEvents} Events", organization.Name, organization.Id, removedProjects, removedStacks, removedEvents);
        }

        private async Task RemoveProjectsAsync(Project project, JobContext context) {
            _logger.LogInformation("Removing project: {Project} ({ProjectId})", project.Name, project.Id);
            await _tokenRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();
            await _webHookRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();
            
            await RenewLockAsync(context).AnyContext();
            long removedEvents = await _eventRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();
            
            await RenewLockAsync(context).AnyContext();
            long removedStacks = await _stackRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();
            
            await _projectRepository.RemoveAsync(project).AnyContext();
            _logger.LogInformation("Removed project: {Project} ({ProjectId}), Removed {RemovedStacks} Stacks, {RemovedEvents} Events", project.Name, project.Id, removedStacks, removedEvents);
        }
        
        private async Task RemoveStackAsync(Stack stack, JobContext context) {
            _logger.LogInformation("Removing stack: {Stack} ({StackId})", stack.Title, stack.Id);
            
            await RenewLockAsync(context).AnyContext();
            long removedEvents = await _eventRepository.RemoveAllByStackIdAsync(stack.OrganizationId, stack.ProjectId, stack.Id).AnyContext();

            await _stackRepository.RemoveAsync(stack).AnyContext();
            _logger.LogInformation("Removed stack: {Stack} ({StackId}), Removed {RemovedEvents} Events", stack.Title, stack.Id, removedEvents);
        }

        private async Task EnforceEventRetentionAsync(JobContext context) {
            var results = await _organizationRepository.GetByRetentionDaysEnabledAsync(o => o.SnapshotPaging().PageLimit(100)).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var organization in results.Documents) {
                    using (_logger.BeginScope(new ExceptionlessState().Organization(organization.Id))) {
                        await EnforceEventCountLimitsAsync(organization, context).AnyContext();

                        // Sleep so we are not hammering the backend.
                        await SystemClock.SleepAsync(TimeSpan.FromSeconds(5)).AnyContext();
                    }
                }

                if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync().AnyContext())
                    break;
            }
        }
        
        private async Task EnforceEventCountLimitsAsync(Organization organization, JobContext context) {
            int retentionDays = organization.RetentionDays;
            if (_appOptions.MaximumRetentionDays > 0 && retentionDays > _appOptions.MaximumRetentionDays)
                retentionDays = _appOptions.MaximumRetentionDays;

            if (retentionDays < 1)
                return;
            
            var cutoff = SystemClock.UtcNow.Date.SubtractDays(retentionDays);
            _logger.LogInformation("Enforcing event count limits older than {RetentionPeriod:g} for organization {OrganizationName} ({OrganizationId}).", cutoff, organization.Name, organization.Id);
            
            await RenewLockAsync(context).AnyContext();
            long removedEvents = await _eventRepository.RemoveAllAsync(organization.Id, null, null, cutoff).AnyContext();
            _logger.LogInformation("Enforced retention period for {OrganizationName} ({OrganizationId}), Removed {RemovedEvents} Events", organization.Name, organization.Id, removedEvents);
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