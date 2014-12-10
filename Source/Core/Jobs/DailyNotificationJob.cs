#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Lock;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Models;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class DailyNotificationJob : JobBase {
        private readonly IProjectRepository _projectRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IStackRepository _stackRepository;
        private readonly EventStats _stats;
        private readonly IMailer _mailer;

        public DailyNotificationJob(IProjectRepository projectRepository, IOrganizationRepository organizationRepository, IUserRepository userRepository, IStackRepository stackRepository, EventStats stats, IMailer mailer, ILockProvider lockProvider) {
            _projectRepository = projectRepository;
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _stackRepository = stackRepository;
            _stats = stats;
            _mailer = mailer;
            LockProvider = lockProvider;
        }

        protected override Task<JobResult> RunInternalAsync(CancellationToken token) {
            Log.Info().Message("Daily Notification job starting").Write();

            if (!Settings.Current.EnableSummaryNotifications)
                return Task.FromResult(new JobResult { Message = "Summary Notifications are disabled.", IsCancelled = true });

            const int BATCH_SIZE = 25;

            // Get all project id's that should be sent at 9:00am in the projects local time.
            var projects = _projectRepository.GetByNextSummaryNotificationOffset(9, BATCH_SIZE);
            while (projects.Count > 0) {
                var documentsUpdated = _projectRepository.IncrementNextSummaryEndOfDayTicks(projects.Select(p => p.Id).ToList());
                Log.Info().Message("Daily Notification job processing {0} projects. Successfully updated {1} projects. ", projects.Count, documentsUpdated);
                Debug.Assert(projects.Count == documentsUpdated);

                foreach (var project in projects) {
                    var utcStartTime = new DateTime(project.NextSummaryEndOfDayTicks - TimeSpan.TicksPerDay);
                    if (utcStartTime < DateTime.UtcNow.Date.SubtractDays(2)) {
                        Log.Info().Message("Skipping Summary Notification older than two days for Project: {0} with a start time of {1}.", project.Id, utcStartTime);
                        continue;
                    }

                    if (_mailer != null) {
                        var notification = new SummaryNotification {
                            Id = project.Id,
                            UtcStartTime = utcStartTime,
                            UtcEndTime = new DateTime(project.NextSummaryEndOfDayTicks - TimeSpan.TicksPerSecond)
                        };

                        Log.Info().Message("Publishing Summary Notification for Project: {0}, with a start time of {1} and an end time of {2}", notification.Id, notification.UtcStartTime, notification.UtcEndTime);
                        ProcessSummaryNotification(notification);
                    } else
                        Log.Error().Message("Mailer is null").Write();
                }

                projects = _projectRepository.GetByNextSummaryNotificationOffset(9, BATCH_SIZE);
            }

            return Task.FromResult(new JobResult { Message = "Successfully sent summary notifications." });
        }

        private void ProcessSummaryNotification(SummaryNotification data) {
            var project = _projectRepository.GetById(data.Id, true);
            var organization = _organizationRepository.GetById(project.OrganizationId, true);
            var userIds = project.NotificationSettings.Where(n => n.Value.SendDailySummary).Select(n => n.Key).ToList();
            if (userIds.Count == 0)
                return;

            var users = _userRepository.GetByIds(userIds).Where(u => u.IsEmailAddressVerified).ToList();
            if (users.Count == 0)
                return;

            long count;
            var paging = new PagingOptions { Limit = 5 };
            List<Stack> newest = _stackRepository.GetNew(project.Id, data.UtcStartTime, data.UtcEndTime, paging).ToList();

            var result = _stats.GetTermsStats(data.UtcStartTime, data.UtcEndTime, "stack_id", "project:" + data.Id, max: 5);
            var mostFrequent = result.Terms.Take(5).ToList();
            var stacks = _stackRepository.GetByIds(mostFrequent.Select(s => s.Term).ToList());

            foreach (var frequent in mostFrequent) {
                var stack = stacks.SingleOrDefault(s => s.Id == frequent.Term);
                if (stack == null) {
                    mostFrequent.RemoveAll(r => r.Term == frequent.Term);
                    continue;
                }

                // Stat's Id and Total properties are already calculated in the Results.
                //frequent.Type = stack.SignatureInfo.ContainsKey("ExceptionType") ? stack.SignatureInfo["ExceptionType"] : null;
                //frequent.Method = stack.SignatureInfo.ContainsKey("Method") ? stack.SignatureInfo["Method"] : null;
                //frequent.Path = stack.SignatureInfo.ContainsKey("Path") ? stack.SignatureInfo["Path"] : null;
                //frequent.Is404 = stack.SignatureInfo.ContainsKey("Path");

                //frequent.Title = stack.Title;
                //frequent.First = stack.FirstOccurrence;
                //frequent.Last = stack.LastOccurrence;
            }

            var notification = new SummaryNotificationModel {
                ProjectId = project.Id,
                ProjectName = project.Name,
                StartDate = data.UtcStartTime,
                EndDate = data.UtcEndTime,
                //Total = result.Total,
                //PerHourAverage = result.PerHourAverage,
                //NewTotal = result.NewTotal,
                //New = newest,
                //UniqueTotal = result.UniqueTotal,
                //MostFrequent = mostFrequent,
                //HasSubmittedErrors = project.TotalErrorCount > 0,
                IsFreePlan = organization.PlanId == BillingManager.FreePlan.Id
            };

            foreach (var user in users.Where(u => u.EmailNotificationsEnabled))
                _mailer.SendSummaryNotification(user.EmailAddress, notification);
        }
    }
}