#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Lock;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class DailyNotificationJob : JobBase {
        private readonly IProjectRepository _projectRepository;
        private readonly IQueue<SummaryNotification> _summaryNotificationQueue;

        public DailyNotificationJob(IProjectRepository projectRepository, IQueue<SummaryNotification> summaryNotificationQueue, ILockProvider lockProvider) {
            _projectRepository = projectRepository;
            _summaryNotificationQueue = summaryNotificationQueue;
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

                    if (_summaryNotificationQueue != null) {
                        var notification = new SummaryNotification {
                            Id = project.Id,
                            UtcStartTime = utcStartTime,
                            UtcEndTime = new DateTime(project.NextSummaryEndOfDayTicks - TimeSpan.TicksPerSecond)
                        };

                        Log.Info().Message("Publishing Summary Notification for Project: {0}, with a start time of {1} and an end time of {2}", notification.Id, notification.UtcStartTime, notification.UtcEndTime);
                        _summaryNotificationQueue.Enqueue(notification);
                    } else
                        Log.Error().Message("Message Factory is null").Write();
                }

                projects = _projectRepository.GetByNextSummaryNotificationOffset(9, BATCH_SIZE);
            }

            return Task.FromResult(new JobResult { Message = "Successfully enforced all retention limits." });
        }
    }
}