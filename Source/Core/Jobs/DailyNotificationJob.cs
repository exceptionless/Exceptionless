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
using CodeSmith.Core.Extensions;
using CodeSmith.Core.Scheduler;
using Exceptionless.Core.Queues;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using NLog.Fluent;
using ServiceStack.Messaging;

namespace Exceptionless.Core.Jobs {
    public class DailyNotificationJob : JobBase {
        private readonly ProjectRepository _projectRepository;
        private readonly IMessageFactory _messageFactory;

        public DailyNotificationJob(ProjectRepository projectRepository, IMessageFactory messageFactory) {
            _projectRepository = projectRepository;
            _messageFactory = messageFactory;
        }

        public override JobResult Run(JobContext context) {
            Log.Info().Message("Daily Notification job starting").Write();

            if (!Settings.Current.EnableSummaryNotifications) {
                return new JobResult {
                    Result = "Summary Notifications are disabled.",
                    Cancelled = true
                };
            }

            const int BATCH_SIZE = 25;

            // Send an email at 9:00am in the projects local time.
            IMongoQuery query = Query.LT(ProjectRepository.FieldNames.NextSummaryEndOfDayTicks, new BsonInt64(DateTime.UtcNow.Ticks - (TimeSpan.TicksPerHour * 9)));
            UpdateBuilder update = Update.Inc(ProjectRepository.FieldNames.NextSummaryEndOfDayTicks, TimeSpan.TicksPerDay);

            var projects = _projectRepository.Collection.FindAs<Project>(query)
                .SetFields(ProjectRepository.FieldNames.Id, ProjectRepository.FieldNames.NextSummaryEndOfDayTicks)
                .SetLimit(BATCH_SIZE).ToList();

            while (projects.Count > 0) {
                IMongoQuery queryWithProjectIds = Query.And(Query.In(ProjectRepository.FieldNames.Id, projects.Select(p => new BsonObjectId(new ObjectId(p.Id)))), query);
                var result = _projectRepository.Collection.Update(queryWithProjectIds, update, UpdateFlags.Multi);
                Log.Info().Message("Daily Notification job processing {0} projects. Successfully updated {1} projects. ", projects.Count, result.DocumentsAffected);

                Debug.Assert(projects.Count == result.DocumentsAffected);

                foreach (var project in projects) {
                    var utcStartTime = new DateTime(project.NextSummaryEndOfDayTicks - TimeSpan.TicksPerDay);
                    if (utcStartTime < DateTime.UtcNow.Date.SubtractDays(2)) {
                        Log.Info().Message("Skipping Summary Notification older than two days for Project: {0} with a start time of {1}.", project.Id, utcStartTime);
                        continue;
                    }

                    if (_messageFactory != null) {
                        using (IMessageProducer messageProducer = _messageFactory.CreateMessageProducer()) {
                            var notification = new SummaryNotification {
                                Id = project.Id,
                                UtcStartTime = utcStartTime,
                                UtcEndTime = new DateTime(project.NextSummaryEndOfDayTicks - TimeSpan.TicksPerSecond)
                            };

                            Log.Info().Message("Publishing Summary Notification for Project: {0}, with a start time of {1} and an end time of {2}", notification.Id, notification.UtcStartTime, notification.UtcEndTime);
                            messageProducer.Publish(notification);
                        }
                    } else
                        Log.Error().Message("Message Factory is null").Write();
                }

                projects = _projectRepository.Collection.FindAs<Project>(query)
                    .SetFields(ProjectRepository.FieldNames.Id, ProjectRepository.FieldNames.NextSummaryEndOfDayTicks)
                    .SetLimit(BATCH_SIZE).ToList();
            }

            return new JobResult {
                Result = "Successfully enforced all retention limits."
            };
        }
    }
}