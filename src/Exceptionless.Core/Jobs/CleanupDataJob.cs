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
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Nest;

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
        private readonly IElasticClient _elasticClient;
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
            IElasticClient elasticClient,
            ICacheClient cacheClient,
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
            _elasticClient = elasticClient;
            _billingManager = billingManager;
            _appOptions = appOptions;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromDays(1));
        }

        protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default) {
            return _lockProvider.AcquireAsync(nameof(StackEventCountJob), TimeSpan.FromHours(2), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            _lastRun = SystemClock.UtcNow;

            await DeleteOrphanedEventsByStackAsync(context).AnyContext();
            await DeleteOrphanedEventsByProjectAsync(context).AnyContext();
            await DeleteOrphanedEventsByOrganizationAsync(context).AnyContext();

            await CleanupSoftDeletedOrganizationsAsync(context).AnyContext();
            await CleanupSoftDeletedProjectsAsync(context).AnyContext();
            await CleanupSoftDeletedStacksAsync(context).AnyContext();

            await EnforceEventRetentionAsync(context).AnyContext();

            _logger.LogInformation("Finished cleaning up data");
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

        private async Task CleanupSoftDeletedStacksAsync(JobContext context) {
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

            var nextPlan = _billingManager.GetBillingPlanByUpsellingRetentionPeriod(organization.RetentionDays);
            if (nextPlan != null)
                retentionDays = nextPlan.RetentionDays;

            var cutoff = SystemClock.UtcNow.Date.SubtractDays(retentionDays);
            _logger.LogInformation("Enforcing event count limits older than {RetentionPeriod:g} for organization {OrganizationName} ({OrganizationId}).", cutoff, organization.Name, organization.Id);

            await RenewLockAsync(context).AnyContext();
            long removedEvents = await _eventRepository.RemoveAllAsync(organization.Id, null, null, cutoff).AnyContext();
            _logger.LogInformation("Enforced retention period for {OrganizationName} ({OrganizationId}), Removed {RemovedEvents} Events", organization.Name, organization.Id, removedEvents);
        }

        public async Task DeleteOrphanedEventsByStackAsync(JobContext context) {
            _logger.LogInformation("Cleaning up Orphaned Events By Stack");
            
            // get approximate number of unique stack ids
            var stackCardinality = await _elasticClient.SearchAsync<PersistentEvent>(s => s.Aggregations(a => a
                .Cardinality("cardinality_stack_id", c => c.Field(f => f.StackId).PrecisionThreshold(40000))));

            var uniqueStackIdCount = stackCardinality.Aggregations.Cardinality("cardinality_stack_id").Value;
            if (!uniqueStackIdCount.HasValue || uniqueStackIdCount.Value <= 0)
                return;

            // break into batches of 500
            const int batchSize = 500;
            int buckets = (int)uniqueStackIdCount.Value / batchSize;
            buckets = Math.Max(1, buckets);

            for (int batchNumber = 0; batchNumber < buckets; batchNumber++) {
                await RenewLockAsync(context).AnyContext();

                var stackIdTerms = await _elasticClient.SearchAsync<PersistentEvent>(s => s.Aggregations(a => a
                    .Terms("terms_stack_id", c => c.Field(f => f.StackId).Include(batchNumber, buckets).Size(batchSize * 2))));

                var stackIds = stackIdTerms.Aggregations.Terms("terms_stack_id").Buckets.Select(b => b.Key).ToArray();
                if (stackIds.Length == 0)
                    continue;

                var stacks = await _elasticClient.MultiGetAsync(r => r.SourceEnabled(false).GetMany<Stack>(stackIds));
                var missingStackIds = stacks.Hits.Where(h => !h.Found).Select(h => h.Id).ToArray();

                if (missingStackIds.Length == 0)
                    continue;

                _logger.LogInformation("Found {OrphanedEventCount} orphaned events from missing stacks {MissingStackIds}", missingStackIds.Length, missingStackIds);
                await _elasticClient.DeleteByQueryAsync<PersistentEvent>(r => r.Query(q => q.Terms(t => t.Field(f => f.StackId).Terms(missingStackIds))));
            }
        }

        public async Task DeleteOrphanedEventsByProjectAsync(JobContext context) {
            _logger.LogInformation("Cleaning up Orphaned Events By Project");
            
            // get approximate number of unique project ids
            var projectCardinality = await _elasticClient.SearchAsync<PersistentEvent>(s => s.Aggregations(a => a
                .Cardinality("cardinality_project_id", c => c.Field(f => f.ProjectId).PrecisionThreshold(40000))));

            var uniqueProjectIdCount = projectCardinality.Aggregations.Cardinality("cardinality_project_id").Value;
            if (!uniqueProjectIdCount.HasValue || uniqueProjectIdCount.Value <= 0)
                return;

            // break into batches of 500
            const int batchSize = 500;
            int buckets = (int)uniqueProjectIdCount.Value / batchSize;
            buckets = Math.Max(1, buckets);

            for (int batchNumber = 0; batchNumber < buckets; batchNumber++) {
                await RenewLockAsync(context).AnyContext();

                var projectIdTerms = await _elasticClient.SearchAsync<PersistentEvent>(s => s.Aggregations(a => a
                    .Terms("terms_project_id", c => c.Field(f => f.ProjectId).Include(batchNumber, buckets).Size(batchSize * 2))));

                var projectIds = projectIdTerms.Aggregations.Terms("terms_project_id").Buckets.Select(b => b.Key).ToArray();
                if (projectIds.Length == 0)
                    continue;

                var projects = await _elasticClient.MultiGetAsync(r => r.SourceEnabled(false).GetMany<Project>(projectIds));
                var missingProjectIds = projects.Hits.Where(h => !h.Found).Select(h => h.Id).ToArray();

                if (missingProjectIds.Length == 0)
                    continue;

                _logger.LogInformation("Found {OrphanedEventCount} orphaned events from missing projects {MissingProjectIds}", missingProjectIds.Length, missingProjectIds);
                await _elasticClient.DeleteByQueryAsync<PersistentEvent>(r => r.Query(q => q.Terms(t => t.Field(f => f.ProjectId).Terms(missingProjectIds))));
            }
        }

        public async Task DeleteOrphanedEventsByOrganizationAsync(JobContext context) {
            _logger.LogInformation("Cleaning up Orphaned Events By Organization");

            // get approximate number of unique organization ids
            var organizationCardinality = await _elasticClient.SearchAsync<PersistentEvent>(s => s.Aggregations(a => a
                .Cardinality("cardinality_organization_id", c => c.Field(f => f.OrganizationId).PrecisionThreshold(40000))));

            var uniqueOrganizationIdCount = organizationCardinality.Aggregations.Cardinality("cardinality_organization_id").Value;
            if (!uniqueOrganizationIdCount.HasValue || uniqueOrganizationIdCount.Value <= 0)
                return;

            // break into batches of 500
            const int batchSize = 500;
            int buckets = (int)uniqueOrganizationIdCount.Value / batchSize;
            buckets = Math.Max(1, buckets);

            for (int batchNumber = 0; batchNumber < buckets; batchNumber++) {
                await RenewLockAsync(context).AnyContext();

                var organizationIdTerms = await _elasticClient.SearchAsync<PersistentEvent>(s => s.Aggregations(a => a
                    .Terms("terms_organization_id", c => c.Field(f => f.OrganizationId).Include(batchNumber, buckets).Size(batchSize * 2))));

                var organizationIds = organizationIdTerms.Aggregations.Terms("terms_organization_id").Buckets.Select(b => b.Key).ToArray();
                if (organizationIds.Length == 0)
                    continue;

                var organizations = await _elasticClient.MultiGetAsync(r => r.SourceEnabled(false).GetMany<Organization>(organizationIds));
                var missingOrganizationIds = organizations.Hits.Where(h => !h.Found).Select(h => h.Id).ToArray();

                if (missingOrganizationIds.Length == 0)
                    continue;

                _logger.LogInformation("Found {OrphanedEventCount} orphaned events from missing organizations {MissingOrganizationIds}", missingOrganizationIds.Length, missingOrganizationIds);
                await _elasticClient.DeleteByQueryAsync<PersistentEvent>(r => r.Query(q => q.Terms(t => t.Field(f => f.OrganizationId).Terms(missingOrganizationIds))));
            }
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