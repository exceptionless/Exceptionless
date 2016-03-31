using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Filter;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Logging;

namespace Exceptionless.Core.Jobs {
    public class DailySummaryJob : JobWithLockBase {
        private readonly IProjectRepository _projectRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IEventRepository _eventRepository;
        private readonly EventStats _stats;
        private readonly IMailer _mailer;
        private readonly ILockProvider _lockProvider;

        public DailySummaryJob(IProjectRepository projectRepository, IOrganizationRepository organizationRepository, IUserRepository userRepository, IEventRepository eventRepository, EventStats stats, IMailer mailer, ICacheClient cacheClient, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _projectRepository = projectRepository;
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _eventRepository = eventRepository;
            _stats = stats;
            _mailer = mailer;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromHours(1));
        }

        protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            return _lockProvider.AcquireAsync(nameof(DailySummaryJob), TimeSpan.FromHours(1), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            if (!Settings.Current.EnableDailySummary)
                return JobResult.SuccessWithMessage("Summary notifications are disabled.");

            if (_mailer == null)
                return JobResult.SuccessWithMessage("Summary notifications are disabled due to null mailer.");

            const int BATCH_SIZE = 25;

            var projects = (await _projectRepository.GetByNextSummaryNotificationOffsetAsync(9, BATCH_SIZE).AnyContext()).Documents;
            while (projects.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                var documentsUpdated = await _projectRepository.IncrementNextSummaryEndOfDayTicksAsync(projects).AnyContext();
                _logger.Info("Got {0} projects to process. ", projects.Count);
                Debug.Assert(projects.Count == documentsUpdated);

                foreach (var project in projects) {
                    var utcStartTime = new DateTime(project.NextSummaryEndOfDayTicks - TimeSpan.TicksPerDay);
                    if (utcStartTime < DateTime.UtcNow.Date.SubtractDays(2)) {
                        _logger.Info("Skipping daily summary older than two days for project \"{0}\" with a start time of \"{1}\".", project.Id, utcStartTime);
                        continue;
                    }

                    var notification = new SummaryNotification {
                        Id = project.Id,
                        UtcStartTime = utcStartTime,
                        UtcEndTime = new DateTime(project.NextSummaryEndOfDayTicks - TimeSpan.TicksPerSecond)
                    };

                    await ProcessSummaryNotificationAsync(notification).AnyContext();

                    // Sleep so were not hammering the database.
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                projects = (await _projectRepository.GetByNextSummaryNotificationOffsetAsync(9, BATCH_SIZE).AnyContext()).Documents;
                if (projects.Count > 0)
                    await context.RenewLockAsync().AnyContext();
            }

            return JobResult.SuccessWithMessage("Successfully sent summary notifications.");
        }

        private async Task ProcessSummaryNotificationAsync(SummaryNotification data) {
            var project = await _projectRepository.GetByIdAsync(data.Id, true).AnyContext();
            var organization = await _organizationRepository.GetByIdAsync(project.OrganizationId, true).AnyContext();
            var userIds = project.NotificationSettings.Where(n => n.Value.SendDailySummary).Select(n => n.Key).ToList();
            if (userIds.Count == 0) {
                _logger.Info("Project \"{0}\" has no users to send summary to.", project.Id);
                return;
            }

            var users = (await _userRepository.GetByIdsAsync(userIds).AnyContext()).Documents.Where(u => u.IsEmailAddressVerified && u.EmailNotificationsEnabled && u.OrganizationIds.Contains(organization.Id)).ToList();
            if (users.Count == 0) {
                _logger.Info("Project \"{0}\" has no users to send summary to.", project.Id);
                return;
            }

            _logger.Info("Sending daily summary: users={0} project={1}", users.Count, project.Id);
            
            var fields = new List<FieldAggregation> {
                new FieldAggregation { Type = FieldAggregationType.Distinct, Field = "stack_id" },
                new TermFieldAggregation { Field = "is_first_occurrence", ExcludePattern = "F" }
            };

            var result = await _stats.GetNumbersStatsAsync(fields, data.UtcStartTime, data.UtcEndTime, $"project:{data.Id} type:error").AnyContext();
            bool hasSubmittedEvents = result.Total > 0;
            if (!hasSubmittedEvents)
                hasSubmittedEvents = await _eventRepository.GetCountByProjectIdAsync(project.Id).AnyContext() > 0;

            var notification = new DailySummaryModel {
                ProjectId = project.Id,
                ProjectName = project.Name,
                StartDate = data.UtcStartTime,
                EndDate = data.UtcEndTime,
                Total = result.Total,
                PerHourAverage = result.Total / data.UtcEndTime.Subtract(data.UtcStartTime).TotalHours,
                NewTotal = result.Numbers[1],
                UniqueTotal = result.Numbers[0],
                HasSubmittedEvents = hasSubmittedEvents,
                IsFreePlan = organization.PlanId == BillingManager.FreePlan.Id
            };

            foreach (var user in users)
                await _mailer.SendDailySummaryAsync(user.EmailAddress, notification).AnyContext();

            _logger.Info("Done sending daily summary: users={0} project={1} events={2}", users.Count, project.Id, notification.Total);
        }
    }
}
