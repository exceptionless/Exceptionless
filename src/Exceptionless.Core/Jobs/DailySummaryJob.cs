using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs {
    [Job(Description = "Sends daily summary emails.", InitialDelay = "1m", Interval = "1h")]
    public class DailySummaryJob : JobWithLockBase, IHealthCheck {
        private readonly EmailOptions _emailOptions;
        private readonly IProjectRepository _projectRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IMailer _mailer;
        private readonly BillingPlans _plans;
        private readonly ILockProvider _lockProvider;
        private DateTime? _lastRun;

        public DailySummaryJob(EmailOptions emailOptions, IProjectRepository projectRepository, IOrganizationRepository organizationRepository, IUserRepository userRepository, IStackRepository stackRepository, IEventRepository eventRepository, IMailer mailer, ICacheClient cacheClient, BillingPlans plans, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _emailOptions = emailOptions;
            _projectRepository = projectRepository;
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _stackRepository = stackRepository;
            _eventRepository = eventRepository;
            _mailer = mailer;
            _plans = plans;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromHours(1));
        }

        protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default) {
            return _lockProvider.AcquireAsync(nameof(DailySummaryJob), TimeSpan.FromHours(1), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            _lastRun = SystemClock.UtcNow;
            
            if (!_emailOptions.EnableDailySummary || _mailer == null)
                return JobResult.SuccessWithMessage("Summary notifications are disabled.");

            var results = await _projectRepository.GetByNextSummaryNotificationOffsetAsync(9).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                _logger.LogTrace("Got {Count} projects to process. ", results.Documents.Count);

                var projectsToBulkUpdate = new List<Project>(results.Documents.Count);
                var processSummariesNewerThan = SystemClock.UtcNow.Date.SubtractDays(2);
                foreach (var project in results.Documents) {
                    using (_logger.BeginScope(new ExceptionlessState().Organization(project.OrganizationId).Project(project.Id))) {
                        var utcStartTime = new DateTime(project.NextSummaryEndOfDayTicks - TimeSpan.TicksPerDay);
                        if (utcStartTime < processSummariesNewerThan) {
                            _logger.LogInformation("Skipping daily summary older than two days for project: {Name}", project.Name);
                            projectsToBulkUpdate.Add(project);
                            continue;
                        }

                        var notification = new SummaryNotification {
                            Id = project.Id,
                            UtcStartTime = utcStartTime,
                            UtcEndTime = new DateTime(project.NextSummaryEndOfDayTicks - TimeSpan.TicksPerSecond)
                        };

                        bool summarySent = await SendSummaryNotificationAsync(project, notification).AnyContext();
                        if (summarySent) {
                            await _projectRepository.IncrementNextSummaryEndOfDayTicksAsync(new[] { project }).AnyContext();

                            // Sleep so we are not hammering the backend as we just generated a report.
                            await SystemClock.SleepAsync(TimeSpan.FromSeconds(2.5)).AnyContext();
                        } else {
                            projectsToBulkUpdate.Add(project);
                        }
                    }
                }

                if (projectsToBulkUpdate.Count > 0) {
                    await _projectRepository.IncrementNextSummaryEndOfDayTicksAsync(projectsToBulkUpdate).AnyContext();

                    // Sleep so we are not hammering the backend
                    await SystemClock.SleepAsync(TimeSpan.FromSeconds(1)).AnyContext();
                }

                if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync().AnyContext())
                    break;

                if (results.Documents.Count > 0) {
                    await context.RenewLockAsync().AnyContext();
                    _lastRun = SystemClock.UtcNow;
                }
            }

            return JobResult.SuccessWithMessage("Successfully sent summary notifications.");
        }

        private async Task<bool> SendSummaryNotificationAsync(Project project, SummaryNotification data) {
            // TODO: Add slack daily summaries
            var userIds = project.NotificationSettings.Where(n => n.Value.SendDailySummary && !String.Equals(n.Key, Project.NotificationIntegrations.Slack)).Select(n => n.Key).ToList();
            if (userIds.Count == 0) {
                _logger.LogInformation("Project {ProjectName} has no users to send summary to.", project.Name);
                return false;
            }

            var results = await _userRepository.GetByIdsAsync(userIds, o => o.Cache()).AnyContext();
            var users = results.Where(u => u.IsEmailAddressVerified && u.EmailNotificationsEnabled && u.OrganizationIds.Contains(project.OrganizationId)).ToList();
            if (users.Count == 0) {
                _logger.LogInformation("Project {ProjectName} has no users to send summary to.", project.Name);
                return false;
            }

            // TODO: What should we do about suspended organizations.
            var organization = await _organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache()).AnyContext();
            if (organization == null) {
                _logger.LogInformation("The organization {organization} for project {ProjectName} may have been deleted. No summaries will be sent.", project.OrganizationId, project.Name);
                return false;
            }

            _logger.LogInformation("Sending daily summary: users={UserCount} project={project}", users.Count, project.Id);
            var sf = new AppFilter(project, organization);
            var systemFilter = new RepositoryQuery<PersistentEvent>().AppFilter(sf).DateRange(data.UtcStartTime, data.UtcEndTime, (PersistentEvent e) => e.Date).Index(data.UtcStartTime, data.UtcEndTime);
            string filter = $"{EventIndex.Alias.Type}:{Event.KnownTypes.Error} {EventIndex.Alias.IsHidden}:false {EventIndex.Alias.IsFixed}:false";
            var result = await _eventRepository.CountBySearchAsync(systemFilter, filter, "terms:(first @include:true) terms:(stack_id~3) cardinality:stack_id sum:count~1").AnyContext();

            double total = result.Aggregations.Sum("sum_count")?.Value ?? result.Total;
            double newTotal = result.Aggregations.Terms<double>("terms_first")?.Buckets.FirstOrDefault()?.Total ?? 0;
            double uniqueTotal = result.Aggregations.Cardinality("cardinality_stack_id")?.Value ?? 0;
            bool hasSubmittedEvents = total > 0 || project.IsConfigured.GetValueOrDefault();
            bool isFreePlan = organization.PlanId == _plans.FreePlan.Id;

            string fixedFilter = $"{EventIndex.Alias.Type}:{Event.KnownTypes.Error} {EventIndex.Alias.IsHidden}:false {EventIndex.Alias.IsFixed}:true";
            var fixedResult = await _eventRepository.CountBySearchAsync(systemFilter, fixedFilter, "sum:count~1").AnyContext();
            double fixedTotal = fixedResult.Aggregations.Sum("sum_count")?.Value ?? fixedResult.Total;

            var range = new DateTimeRange(data.UtcStartTime, data.UtcEndTime);
            var usages = project.OverageHours.Where(u => range.Contains(u.Date)).ToList();
            int blockedTotal = usages.Sum(u => u.Blocked);
            int tooBigTotal = usages.Sum(u => u.TooBig);

            IReadOnlyCollection<Stack> mostFrequent = null;
            var stackTerms = result.Aggregations.Terms<string>("terms_stack_id");
            if (stackTerms?.Buckets.Count > 0)
                mostFrequent = await _stackRepository.GetByIdsAsync(stackTerms.Buckets.Select(b => b.Key).ToArray()).AnyContext();

            IReadOnlyCollection<Stack> newest = null;
            if (newTotal > 0)
                newest = (await _stackRepository.GetByFilterAsync(sf, filter, "-first", "first", data.UtcStartTime, data.UtcEndTime, o => o.PageLimit(3)).AnyContext()).Documents;

            foreach (var user in users) {
                _logger.LogInformation("Queuing {ProjectName} daily summary email ({UtcStartTime}-{UtcEndTime}) for user {EmailAddress}.", project.Name, data.UtcStartTime, data.UtcEndTime, user.EmailAddress);
                await _mailer.SendProjectDailySummaryAsync(user, project, mostFrequent, newest, data.UtcStartTime, hasSubmittedEvents, total, uniqueTotal, newTotal, fixedTotal, blockedTotal, tooBigTotal, isFreePlan).AnyContext();
            }

            _logger.LogInformation("Done sending daily summary: users={UserCount} project={ProjectName} events={EventCount}", users.Count, project.Name, total);
            return true;
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
