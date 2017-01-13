using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Logging;
using Foundatio.Repositories.Elasticsearch.Queries;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Foundatio.Utility;

namespace Exceptionless.Core.Jobs {
    public class DailySummaryJob : JobWithLockBase {
        private readonly IProjectRepository _projectRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IMailer _mailer;
        private readonly ILockProvider _lockProvider;

        public DailySummaryJob(IProjectRepository projectRepository, IOrganizationRepository organizationRepository, IUserRepository userRepository, IEventRepository eventRepository, IMailer mailer, ICacheClient cacheClient, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _projectRepository = projectRepository;
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _eventRepository = eventRepository;
            _mailer = mailer;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromHours(1));
        }

        protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            return _lockProvider.AcquireAsync(nameof(DailySummaryJob), TimeSpan.FromHours(1), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            if (!Settings.Current.EnableDailySummary || _mailer == null)
                return JobResult.SuccessWithMessage("Summary notifications are disabled.");

            var results = await _projectRepository.GetByNextSummaryNotificationOffsetAsync(9).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                _logger.Info("Got {0} projects to process. ", results.Documents.Count);

                var projectsToBulkUpdate = new List<Project>(results.Documents.Count);
                var processSummariesNewerThan = SystemClock.UtcNow.Date.SubtractDays(2);
                foreach (var project in results.Documents) {
                    var utcStartTime = new DateTime(project.NextSummaryEndOfDayTicks - TimeSpan.TicksPerDay);
                    if (utcStartTime < processSummariesNewerThan) {
                        _logger.Info().Project(project.Id).Message("Skipping daily summary older than two days for project: {0}", project.Name).Write();
                        projectsToBulkUpdate.Add(project);
                        continue;
                    }

                    var notification = new SummaryNotification {
                        Id = project.Id,
                        UtcStartTime = utcStartTime,
                        UtcEndTime = new DateTime(project.NextSummaryEndOfDayTicks - TimeSpan.TicksPerSecond)
                    };

                    var summarySent = await SendSummaryNotificationAsync(project, notification).AnyContext();
                    if (summarySent) {
                        await _projectRepository.IncrementNextSummaryEndOfDayTicksAsync(new[] { project }).AnyContext();

                        // Sleep so we are not hammering the backend as we just generated a report.
                        await SystemClock.SleepAsync(TimeSpan.FromSeconds(2.5)).AnyContext();
                    } else {
                        projectsToBulkUpdate.Add(project);
                    }
                }

                if (projectsToBulkUpdate.Count > 0) {
                    await _projectRepository.IncrementNextSummaryEndOfDayTicksAsync(projectsToBulkUpdate).AnyContext();

                    // Sleep so we are not hammering the backend
                    await SystemClock.SleepAsync(TimeSpan.FromSeconds(1)).AnyContext();
                }

                if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync().AnyContext())
                    break;

                if (results.Documents.Count > 0)
                    await context.RenewLockAsync().AnyContext();
            }

            return JobResult.SuccessWithMessage("Successfully sent summary notifications.");
        }

        private async Task<bool> SendSummaryNotificationAsync(Project project, SummaryNotification data) {
            var userIds = project.NotificationSettings.Where(n => n.Value.SendDailySummary).Select(n => n.Key).ToList();
            if (userIds.Count == 0) {
                _logger.Info().Project(project.Id).Message("Project \"{0}\" has no users to send summary to.", project.Name).Write();
                return false;
            }

            var results = await _userRepository.GetByIdsAsync(userIds, true).AnyContext();
            var users = results.Where(u => u.IsEmailAddressVerified && u.EmailNotificationsEnabled && u.OrganizationIds.Contains(project.OrganizationId)).ToList();
            if (users.Count == 0) {
                _logger.Info().Project(project.Id).Message("Project \"{0}\" has no users to send summary to.", project.Name);
                return false;
            }

            // TODO: What should we do about suspended organizations.
            var organization = await _organizationRepository.GetByIdAsync(project.OrganizationId, true).AnyContext();
            if (organization == null) {
                _logger.Info().Project(project.Id).Message("The organization \"{0}\" for project \"{1}\" may have been deleted. No summaries will be sent.", project.OrganizationId, project.Name);
                return false;
            }

            _logger.Info("Sending daily summary: users={0} project={1}", users.Count, project.Id);
            var sf = new ExceptionlessSystemFilterQuery(project, organization);
            var systemFilter = new ElasticQuery().WithSystemFilter(sf).WithDateRange(data.UtcStartTime, data.UtcEndTime, (PersistentEvent e) => e.Date).WithIndexes(data.UtcStartTime, data.UtcEndTime);
            var result = await _eventRepository.CountBySearchAsync(systemFilter, $"{EventIndexType.Alias.Type}:{Event.KnownTypes.Error}", "terms:(is_first_occurrence @include:true) cardinality:stack_id").AnyContext();
            bool hasSubmittedEvents = result.Total > 0;
            if (!hasSubmittedEvents)
                hasSubmittedEvents = await _eventRepository.GetCountByProjectIdAsync(project.Id, true).AnyContext() > 0;

            var notification = new DailySummaryModel {
                ProjectId = project.Id,
                ProjectName = project.Name,
                StartDate = data.UtcStartTime,
                EndDate = data.UtcEndTime,
                Total = result.Total,
                PerHourAverage = result.Total / data.UtcEndTime.Subtract(data.UtcStartTime).TotalHours,
                NewTotal = result.Aggregations.Terms<double>("terms_is_first_occurrence")?.Buckets.First().Total ?? 0,
                UniqueTotal = result.Aggregations.Cardinality("cardinality_stack_id")?.Value ?? 0,
                HasSubmittedEvents = hasSubmittedEvents,
                IsFreePlan = organization.PlanId == BillingManager.FreePlan.Id
            };

            foreach (var user in users)
                await _mailer.SendDailySummaryAsync(user.EmailAddress, notification).AnyContext();

            _logger.Info().Project(project.Id).Message("Done sending daily summary: users={0} project={1} events={2}", users.Count, project.Name, notification.Total);
            return true;
        }
    }
}
