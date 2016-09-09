using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Filter;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Logging;
using Foundatio.Utility;

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
            var results = await _projectRepository.GetByNextSummaryNotificationOffsetAsync(9, BATCH_SIZE).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                _logger.Info("Got {0} projects to process. ", results.Documents.Count);

                foreach (var project in results.Documents) {
                    var utcStartTime = new DateTime(project.NextSummaryEndOfDayTicks);
                    if (utcStartTime < SystemClock.UtcNow.Date.SubtractDays(2)) {
                        _logger.Info("Skipping daily summary older than two days for project \"{0}\" with a start time of \"{1}\".", project.Id, utcStartTime);
                        await _projectRepository.IncrementNextSummaryEndOfDayTicksAsync(new[] { project }).AnyContext();
                        continue;
                    }

                    var notification = new SummaryNotification {
                        Id = project.Id,
                        UtcStartTime = utcStartTime,
                        UtcEndTime = new DateTime(project.NextSummaryEndOfDayTicks - TimeSpan.TicksPerSecond)
                    };

                    await ProcessSummaryNotificationAsync(notification).AnyContext();
                    await _projectRepository.IncrementNextSummaryEndOfDayTicksAsync(new[] { project }).AnyContext();

                    // Sleep so we are not hammering the backend.
                    await SystemClock.SleepAsync(TimeSpan.FromSeconds(1)).AnyContext();
                }
                
                if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync().AnyContext())
                    break;
                
                if (results.Documents.Count > 0)
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

            var users = (await _userRepository.GetByIdsAsync(userIds).AnyContext()).Where(u => u.IsEmailAddressVerified && u.EmailNotificationsEnabled && u.OrganizationIds.Contains(organization.Id)).ToList();
            if (users.Count == 0) {
                _logger.Info("Project \"{0}\" has no users to send summary to.", project.Id);
                return;
            }

            _logger.Info("Sending daily summary: users={0} project={1}", users.Count, project.Id);

            var fields = new List<FieldAggregation> {
                new FieldAggregation { Type = FieldAggregationType.Distinct, Field = "stack_id" },
                new TermFieldAggregation { Field = "is_first_occurrence", ExcludePattern = "F" }
            };

            var sf = new ExceptionlessSystemFilterQuery(project, organization);
            var result = await _stats.GetNumbersStatsAsync(fields, data.UtcStartTime, data.UtcEndTime, sf, $"{EventIndexType.Fields.Type}:{Event.KnownTypes.Error}").AnyContext();
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
