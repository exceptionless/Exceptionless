using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Logging;

namespace Exceptionless.Core.Jobs {
    public class DailySummaryJob : JobBase {
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

        protected override Task<ILock> GetJobLockAsync() {
            return _lockProvider.AcquireAsync(nameof(DailySummaryJob), TimeSpan.FromHours(1), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobRunContext context) {
            if (!Settings.Current.EnableDailySummary)
                return JobResult.SuccessWithMessage("Summary notifications are disabled.");

            if (_mailer == null)
                return JobResult.SuccessWithMessage("Summary notifications are disabled due to null mailer.");

            const int BATCH_SIZE = 25;

            var projects = (await _projectRepository.GetByNextSummaryNotificationOffsetAsync(9, BATCH_SIZE).AnyContext()).Documents;
            while (projects.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                var documentsUpdated = await _projectRepository.IncrementNextSummaryEndOfDayTicksAsync(projects.Select(p => p.Id).ToList()).AnyContext();
                _logger.Info().Message("Got {0} projects to process. ", projects.Count).Write();
                Debug.Assert(projects.Count == documentsUpdated);

                foreach (var project in projects) {
                    var utcStartTime = new DateTime(project.NextSummaryEndOfDayTicks - TimeSpan.TicksPerDay);
                    if (utcStartTime < DateTime.UtcNow.Date.SubtractDays(2)) {
                        _logger.Info().Message("Skipping daily summary older than two days for project \"{0}\" with a start time of \"{1}\".", project.Id, utcStartTime).Write();
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
                    await context.JobLock.RenewAsync().AnyContext();
            }

            return JobResult.SuccessWithMessage("Successfully sent summary notifications.");
        }

        private async Task ProcessSummaryNotificationAsync(SummaryNotification data) {
            var project = await _projectRepository.GetByIdAsync(data.Id, true).AnyContext();
            var organization = await _organizationRepository.GetByIdAsync(project.OrganizationId, true).AnyContext();
            var userIds = project.NotificationSettings.Where(n => n.Value.SendDailySummary).Select(n => n.Key).ToList();
            if (userIds.Count == 0) {
                _logger.Info().Message("Project \"{0}\" has no users to send summary to.", project.Id).Write();
                return;
            }

            var users = (await _userRepository.GetByIdsAsync(userIds).AnyContext()).Documents.Where(u => u.IsEmailAddressVerified && u.EmailNotificationsEnabled && u.OrganizationIds.Contains(organization.Id)).ToList();
            if (users.Count == 0) {
                _logger.Info().Message("Project \"{0}\" has no users to send summary to.", project.Id).Write();
                return;
            }

            _logger.Info().Message("Sending daily summary: users={0} project={1}", users.Count, project.Id).Write();
            //var paging = new PagingOptions { Limit = 5 };
            //List<Stack> newest = (await _stackRepository.GetNewAsync(project.Id, data.UtcStartTime, data.UtcEndTime, paging).AnyContext()).Documents.ToList();
            var newest = new List<Stack>();

            var result = await _stats.GetTermsStatsAsync(data.UtcStartTime, data.UtcEndTime, "stack_id", "type:error project:" + data.Id, max: 5).AnyContext();
            //var termStatsList = result.Terms.Take(5).ToList();
            //var stacks = _stackRepository.GetByIds(termStatsList.Select(s => s.Term).ToList());
            bool hasSubmittedErrors = result.Total > 0;
            if (!hasSubmittedErrors)
                hasSubmittedErrors = await _eventRepository.GetCountByProjectIdAsync(project.Id).AnyContext() > 0;

            var mostFrequent = new List<EventStackResult>();
            //foreach (var termStats in termStatsList) {
            //    var stack = stacks.SingleOrDefault(s => s.Id == termStats.Term);
            //    if (stack == null)
            //        continue;

            //    mostFrequent.Add(new EventStackResult {
            //        First =  termStats.FirstOccurrence,
            //        Last = termStats.LastOccurrence,
            //        Id = stack.Id,
            //        Title = stack.Title,
            //        Total = termStats.Total,
            //        Type = stack.SignatureInfo.ContainsKey("ExceptionType") ? stack.SignatureInfo["ExceptionType"] : null,
            //        Method = stack.SignatureInfo.ContainsKey("Method") ? stack.SignatureInfo["Method"] : null,
            //        Path = stack.SignatureInfo.ContainsKey("Source") ? stack.SignatureInfo["Source"] : null,
            //        Is404 = stack.SignatureInfo.ContainsKey("Type") && stack.SignatureInfo["Type"] == "404"
            //    });
            //}

            var notification = new DailySummaryModel {
                ProjectId = project.Id,
                ProjectName = project.Name,
                StartDate = data.UtcStartTime,
                EndDate = data.UtcEndTime,
                Total = result.Total,
                PerHourAverage = result.Total / data.UtcEndTime.Subtract(data.UtcStartTime).TotalHours,
                NewTotal = result.New,
                New = newest,
                UniqueTotal = result.Unique,
                MostFrequent = mostFrequent,
                HasSubmittedEvents = hasSubmittedErrors,
                IsFreePlan = organization.PlanId == BillingManager.FreePlan.Id
            };

            foreach (var user in users)
                await _mailer.SendDailySummaryAsync(user.EmailAddress, notification).AnyContext();

            _logger.Info().Message("Done sending daily summary: users={0} project={1} events={2}", users.Count, project.Id, notification.Total).Write();
        }
    }
}
