using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models;
using Foundatio.Jobs;
using Foundatio.Lock;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class DailySummaryJob : JobBase {
        private readonly IProjectRepository _projectRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;
        private readonly EventStats _stats;
        private readonly IMailer _mailer;
        private readonly ILockProvider _lockProvider;

        public DailySummaryJob(IProjectRepository projectRepository, IOrganizationRepository organizationRepository, IUserRepository userRepository, IStackRepository stackRepository, IEventRepository eventRepository, EventStats stats, IMailer mailer, ILockProvider lockProvider) {
            _projectRepository = projectRepository;
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _stackRepository = stackRepository;
            _eventRepository = eventRepository;
            _stats = stats;
            _mailer = mailer;
            _lockProvider = lockProvider;
        }

        protected override IDisposable GetJobLock() {
            return _lockProvider.AcquireLock("DailySummaryJob");
        }

        protected override Task<JobResult> RunInternalAsync(CancellationToken token) {
            if (!Settings.Current.EnableDailySummary)
                return Task.FromResult(JobResult.SuccessWithMessage("Summary notifications are disabled."));

            if (_mailer == null)
                return Task.FromResult(JobResult.SuccessWithMessage("Summary notifications are disabled due to null mailer."));

            const int BATCH_SIZE = 25;

            var projects = _projectRepository.GetByNextSummaryNotificationOffset(9, BATCH_SIZE);
            while (projects.Count > 0 && !token.IsCancellationRequested) {
                var documentsUpdated = _projectRepository.IncrementNextSummaryEndOfDayTicks(projects.Select(p => p.Id).ToList());
                Log.Info().Message("Got {0} projects to process. ", projects.Count).Write();
                Debug.Assert(projects.Count == documentsUpdated);

                foreach (var project in projects) {
                    var utcStartTime = new DateTime(project.NextSummaryEndOfDayTicks - TimeSpan.TicksPerDay);
                    if (utcStartTime < DateTime.UtcNow.Date.SubtractDays(2)) {
                        Log.Info().Message("Skipping daily summary older than two days for project \"{0}\" with a start time of \"{1}\".", project.Id, utcStartTime).Write();
                        continue;
                    }

                    var notification = new SummaryNotification {
                        Id = project.Id,
                        UtcStartTime = utcStartTime,
                        UtcEndTime = new DateTime(project.NextSummaryEndOfDayTicks - TimeSpan.TicksPerSecond)
                    };

                    ProcessSummaryNotification(notification);
                }

                projects = _projectRepository.GetByNextSummaryNotificationOffset(9, BATCH_SIZE);
            }

            return Task.FromResult(new JobResult { Message = "Successfully sent summary notifications." });
        }

        private void ProcessSummaryNotification(SummaryNotification data) {
            var project = _projectRepository.GetById(data.Id, true);
            var organization = _organizationRepository.GetById(project.OrganizationId, true);
            var userIds = project.NotificationSettings.Where(n => n.Value.SendDailySummary).Select(n => n.Key).ToList();
            if (userIds.Count == 0) {
                Log.Info().Message("Project \"{0}\" has no users to send summary to.", project.Id).Write();
                return;
            }

            var users = _userRepository.GetByIds(userIds).Where(u => u.IsEmailAddressVerified && u.EmailNotificationsEnabled && u.OrganizationIds.Contains(organization.Id)).ToList();
            if (users.Count == 0) {
                Log.Info().Message("Project \"{0}\" has no users to send summary to.", project.Id).Write();
                return;
            }

            Log.Info().Message("Sending daily summary: users={0} project={1}", users.Count, project.Id).Write();
            var paging = new PagingOptions { Limit = 5 };
            List<Stack> newest = _stackRepository.GetNew(project.Id, data.UtcStartTime, data.UtcEndTime, paging).ToList();

            var result = _stats.GetTermsStats(data.UtcStartTime, data.UtcEndTime, "stack_id", "type:error project:" + data.Id, max: 5);
            //var termStatsList = result.Terms.Take(5).ToList();
            //var stacks = _stackRepository.GetByIds(termStatsList.Select(s => s.Term).ToList());
            bool hasSubmittedErrors = result.Total > 0;
            if (!hasSubmittedErrors)
                hasSubmittedErrors = _eventRepository.GetCountByProjectId(project.Id) > 0;

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
                _mailer.SendDailySummary(user.EmailAddress, notification);
            
            Log.Info().Message("Done sending daily summary: users={0} project={1} events={2}", users.Count, project.Id, notification.Total).Write();
        }
    }
}