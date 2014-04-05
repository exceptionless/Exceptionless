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
using System.Linq;
using System.Net;
using System.Net.Http;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using MongoDB.Driver.Builders;
using NLog.Fluent;
using ServiceStack.CacheAccess;
using ServiceStack.Messaging;
using ServiceStack.Redis;
using ServiceStack.Redis.Messaging;
using UAParser;

namespace Exceptionless.Core.Queues {
    public class ExceptionlessMqServer : RedisMqServer {
        private readonly IProjectRepository _projectRepository;
        private readonly IProjectHookRepository _projectHookRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly EventPipeline _eventPipeline;
        private readonly IUserRepository _userRepository;
        private readonly EventStatsHelper _eventStatsHelper;
        private readonly ICacheClient _cacheClient;
        private readonly IMailer _mailer;
        private readonly IAppStatsClient _stats;

        public ExceptionlessMqServer(IRedisClientsManager clientsManager, IProjectRepository projectRepository, IUserRepository userRepository,
            IStackRepository stackRepository, IOrganizationRepository organizationRepository, EventPipeline eventPipeline,
            EventStatsHelper eventStatsHelper, IProjectHookRepository projectHookRepository, ICacheClient cacheClient, IMailer mailer, IAppStatsClient stats)
            : base(clientsManager) {
            _projectRepository = projectRepository;
            _projectHookRepository = projectHookRepository;
            _userRepository = userRepository;
            _stackRepository = stackRepository;
            _organizationRepository = organizationRepository;
            _eventPipeline = eventPipeline;
            _eventStatsHelper = eventStatsHelper;
            _cacheClient = cacheClient;
            _mailer = mailer;
            _stats = stats;

            RegisterHandler<SummaryNotification>(ProcessSummaryNotification, ProcessSummaryNotificationException);
            RegisterHandler<EventNotification>(ProcessNotification, ProcessNotificationException);
            RegisterHandler<Event>(ProcessEvent, ProcessEventException);
            RegisterHandler<WebHookNotification>(ProcessWebHookNotification, ProcessWebHookNotificationException);
        }

        private void ProcessWebHookNotificationException(IMessage<WebHookNotification> message, Exception exception) {
            Log.Error().Project(message.GetBody().ProjectId).Exception(exception).Message("Error calling web hook ({0}): {1}", message.GetBody().Url, exception.Message).Write();
        }

        private object ProcessWebHookNotification(IMessage<WebHookNotification> message) {
            WebHookNotification body = message.GetBody();
            Log.Trace().Project(body.ProjectId).Message("Process web hook call: project={0} url={1}", body.ProjectId, body.Url).Write();

            var client = new HttpClient();
            client.PostAsJsonAsync(body.Url, body.Data).ContinueWith(res => {
                if (res.Result.StatusCode == HttpStatusCode.Gone) {
                    _projectHookRepository.Delete(Query.EQ(ProjectHookRepository.FieldNames.Url, body.Url));
                    Log.Trace().Project(body.ProjectId).Message("Deleting web hook: project={0} url={1}", body.ProjectId, body.Url).Write();
                }

                Log.Trace().Project(body.ProjectId).Message("Web hook POST complete: status={0} project={1} url={2}", res.Result.StatusCode, body.ProjectId, body.Url).Write();
            }).Wait();

            return null;
        }

        private void ProcessSummaryNotificationException(IMessage<SummaryNotification> message, Exception exception) {
            exception.ToExceptionless().AddDefaultInformation().MarkAsCritical().AddObject(message.GetBody()).AddTags("ErrorMQ").Submit();
            Log.Error().Project(message.GetBody().Id).Exception(exception).Message("Error processing daily summary.").Write();
        }

        private object ProcessSummaryNotification(IMessage<SummaryNotification> message) {
            var project = _projectRepository.GetByIdCached(message.GetBody().Id);
            var organization = _organizationRepository.GetByIdCached(project.OrganizationId);
            var userIds = project.NotificationSettings.Where(n => n.Value.SendDailySummary).Select(n => n.Key).ToList();
            if (userIds.Count == 0)
                return null;

            var users = _userRepository.GetByIds(userIds).Where(u => u.IsEmailAddressVerified).ToList();
            if (users.Count == 0)
                return null;

            long count;
            List<Stack> newest = _stackRepository.GetNew(project.Id, message.GetBody().UtcStartTime, message.GetBody().UtcEndTime, 0, 5, out count).ToList();

            DateTime start = _projectRepository.UtcToDefaultProjectLocalTime(project.Id, message.GetBody().UtcStartTime);
            DateTime end = _projectRepository.UtcToDefaultProjectLocalTime(project.Id, message.GetBody().UtcEndTime);
            var result = _eventStatsHelper.GetProjectErrorStats(project.Id, _projectRepository.GetDefaultTimeOffset(project.Id), start, end);
            var mostFrequent = result.MostFrequent.Results.Take(5).ToList();
            var errorStacks = _stackRepository.GetByIds(mostFrequent.Select(s => s.Id));

            foreach (var frequent in mostFrequent) {
                var stack = errorStacks.SingleOrDefault(s => s.Id == frequent.Id);
                if (stack == null) {
                    mostFrequent.RemoveAll(r => r.Id == frequent.Id);
                    continue;
                }

                // Stat's Id and Total properties are already calculated in the Results.
                frequent.Type = stack.SignatureInfo.ContainsKey("ExceptionType") ? stack.SignatureInfo["ExceptionType"] : null;
                frequent.Method = stack.SignatureInfo.ContainsKey("Method") ? stack.SignatureInfo["Method"] : null;
                frequent.Path = stack.SignatureInfo.ContainsKey("Path") ? stack.SignatureInfo["Path"] : null;
                frequent.Is404 = stack.SignatureInfo.ContainsKey("Path");

                frequent.Title = stack.Title;
                frequent.First = stack.FirstOccurrence;
                frequent.Last = stack.LastOccurrence;
            }

            var notification = new SummaryNotificationModel {
                ProjectId = project.Id,
                ProjectName = project.Name,
                StartDate = start,
                EndDate = end,
                Total = result.Total,
                PerHourAverage = result.PerHourAverage,
                NewTotal = result.NewTotal,
                New = newest,
                UniqueTotal = result.UniqueTotal,
                MostFrequent = mostFrequent,
                HasSubmittedErrors = project.TotalErrorCount > 0,
                IsFreePlan = organization.PlanId == BillingManager.FreePlan.Id
            };

            foreach (var user in users.Where(u => u.EmailNotificationsEnabled))
                _mailer.SendSummaryNotification(user.EmailAddress, notification);

            return null;
        }

        private void ProcessEventException(IMessage<Event> message, Exception exception) {
            exception.ToExceptionless().AddDefaultInformation().MarkAsCritical().AddObject(message.GetBody()).AddTags("ErrorMQ").Submit();
            Log.Error().Project(message.GetBody().ProjectId).Exception(exception).Message("Error processing error.").Write();
            _stats.Counter(StatNames.ErrorsProcessingFailed);
        }

        private object ProcessEvent(IMessage<Event> message) {
            Event value = message.GetBody();
            if (value == null)
                return null;

            _stats.Counter(StatNames.ErrorsDequeued);
            using (_stats.StartTimer(StatNames.ErrorsProcessingTime))
                _eventPipeline.Run(value);

            return null;
        }

        private void ProcessNotificationException(IMessage<EventNotification> message, Exception exception) {
            exception.ToExceptionless().AddDefaultInformation().MarkAsCritical().AddObject(message.GetBody()).AddTags("NotificationMQ").Submit();
            Log.Error().Project(message.GetBody().ProjectId).Exception(exception).Message("Error sending notification.").Write();
        }

        private object ProcessNotification(IMessage<EventNotification> message) {
            int emailsSent = 0;
            EventNotification eventNotification = message.GetBody();
            Log.Trace().Message("Process notification: project={0} event={1} stack={2}", eventNotification.ProjectId, eventNotification.EventId, eventNotification.StackId).Write();

            var project = _projectRepository.GetByIdCached(eventNotification.ProjectId);
            if (project == null) {
                Log.Error().Message("Could not load project {0}.", eventNotification.ProjectId).Write();
                return null;
            }
            Log.Trace().Message("Loaded project: name={0}", project.Name).Write();

            var organization = _organizationRepository.GetByIdCached(project.OrganizationId);
            if (organization == null) {
                Log.Error().Message("Could not load organization {0}.", project.OrganizationId).Write();
                return null;
            }
            Log.Trace().Message("Loaded organization: name={0}", organization.Name).Write();

            var stack = _stackRepository.GetById(eventNotification.StackId);
            if (stack == null) {
                Log.Error().Message("Could not load stack {0}.", eventNotification.StackId).Write();
                return null;
            }

            if (!organization.HasPremiumFeatures) {
                Log.Trace().Message("Skipping because organization does not have premium features.").Write();
                return null;
            }

            if (stack.DisableNotifications || stack.IsHidden) {
                Log.Trace().Message("Skipping because stack notifications are disabled or it's hidden.").Write();
                return null;
            }

            Log.Trace().Message("Loaded stack: title={0}", stack.Title).Write();
            int totalOccurrences = stack.TotalOccurrences;

            // after the first 5 occurrences, don't send a notification for the same stack more then once every 15 minutes
            var lastTimeSent = _cacheClient.Get<DateTime>(String.Concat("NOTIFICATION_THROTTLE_", eventNotification.StackId));
            if (totalOccurrences > 5 && !eventNotification.IsRegression && lastTimeSent != DateTime.MinValue &&
                lastTimeSent > DateTime.Now.AddMinutes(-15)) {
                Log.Info().Message("Skipping message because of throttling: last sent={0} occurrences={1}", lastTimeSent, totalOccurrences).Write();
                return null;
            }

            foreach (var kv in project.NotificationSettings) {
                var settings = kv.Value;
                Log.Trace().Message("Processing notification: user={0}", kv.Key).Write();

                var user = _userRepository.GetById(kv.Key);
                if (user == null || String.IsNullOrEmpty(user.EmailAddress)) {
                    Log.Error().Message("Could not load user {0} or blank email address {1}.", kv.Key, user != null ? user.EmailAddress : "").Write();
                    continue;
                }

                if (!user.IsEmailAddressVerified) {
                    Log.Info().Message("User {0} with email address {1} has not been verified.", kv.Key, user != null ? user.EmailAddress : "").Write();
                    continue;
                }

                if (!user.EmailNotificationsEnabled) {
                    Log.Trace().Message("User {0} with email address {1} has email notifications disabled.", kv.Key, user != null ? user.EmailAddress : "").Write();
                    continue;
                }

                if (!user.OrganizationIds.Contains(project.OrganizationId)) {
                    // TODO: Should this notification setting be deleted?
                    Log.Error().Message("Unauthorized user: project={0} user={1} organization={2} event={3}", project.Id, kv.Key,
                        project.OrganizationId, eventNotification.EventId).Write();
                    continue;
                }

                Log.Trace().Message("Loaded user: email={0}", user.EmailAddress).Write();

                bool shouldReportOccurrence = settings.Mode != NotificationMode.None;
                bool shouldReportCriticalError = settings.ReportCriticalErrors && eventNotification.IsCritical;
                bool shouldReportRegression = settings.ReportRegressions && eventNotification.IsRegression;

                Log.Trace().Message("Settings: mode={0} critical={1} regression={2} 404={3} bots={4}",
                    settings.Mode, settings.ReportCriticalErrors,
                    settings.ReportRegressions, settings.Report404Errors,
                    settings.ReportKnownBotErrors).Write();
                Log.Trace().Message("Should process: occurrence={0} critical={1} regression={2}",
                    shouldReportOccurrence, shouldReportCriticalError,
                    shouldReportRegression).Write();

                if (settings.Mode == NotificationMode.New && !eventNotification.IsNew) {
                    shouldReportOccurrence = false;
                    Log.Trace().Message("Skipping because message is not new.").Write();
                }

                // check for 404s if the user has elected to not report them
                if (shouldReportOccurrence && settings.Report404Errors == false && eventNotification.Code == "404") {
                    shouldReportOccurrence = false;
                    Log.Trace().Message("Skipping because message is 404.").Write();
                }

                // check for known bots if the user has elected to not report them
                if (shouldReportOccurrence && settings.ReportKnownBotErrors == false &&
                    !String.IsNullOrEmpty(eventNotification.UserAgent)) {
                    ClientInfo info = null;
                    try {
                        info = Parser.GetDefault().Parse(eventNotification.UserAgent);
                    } catch (Exception ex) {
                        Log.Warn().Project(eventNotification.ProjectId).Message("Unable to parse user agent {0}. Exception: {1}",
                            eventNotification.UserAgent, ex.Message).Write();
                    }

                    if (info != null && info.Device.IsSpider) {
                        shouldReportOccurrence = false;
                        Log.Trace().Message("Skipping because message is bot.").Write();
                    }
                }

                // stack being set to send all will override all other settings
                if (!shouldReportOccurrence && !shouldReportCriticalError && !shouldReportRegression)
                    continue;

                var model = new EventNotificationModel(eventNotification) {
                    ProjectName = project.Name,
                    TotalOccurrences = totalOccurrences
                };

                // don't send notifications in non-production mode to email addresses that are not on the outbound email list.
                if (Settings.Current.WebsiteMode != WebsiteMode.Production
                    && !Settings.Current.AllowedOutboundAddresses.Contains(v => user.EmailAddress.ToLowerInvariant().Contains(v))) {
                    Log.Trace().Message("Skipping because email is not on the outbound list and not in production mode.").Write();
                    continue;
                }

                Log.Trace().Message("Sending email to {0}...", user.EmailAddress).Write();
                _mailer.SendNotice(user.EmailAddress, model);
                emailsSent++;
                Log.Trace().Message("Done sending email.").Write();
            }

            // if we sent any emails, mark the last time a notification for this stack was sent.
            if (emailsSent > 0)
                _cacheClient.Set(String.Concat("NOTIFICATION_THROTTLE_", eventNotification.StackId), DateTime.Now, DateTime.Now.AddMinutes(15));

            return null;
        }
    }
}