using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Queues;
using NLog.Fluent;
using UAParser;
#pragma warning disable 1998

namespace Exceptionless.Core.Jobs {
    public class EventNotificationsJob : JobBase {
        private readonly IQueue<EventNotificationWorkItem> _queue;
        private readonly IMailer _mailer;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IUserRepository _userRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ICacheClient _cacheClient;

        public EventNotificationsJob(IQueue<EventNotificationWorkItem> queue, IMailer mailer,
            IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IStackRepository stackRepository,
            IUserRepository userRepository, IEventRepository eventRepository, ICacheClient cacheClient) {
            _queue = queue;
            _mailer = mailer;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
            _userRepository = userRepository;
            _eventRepository = eventRepository;
            _cacheClient = cacheClient;
        }

        protected async override Task<JobResult> RunInternalAsync(CancellationToken token) {
            QueueEntry<EventNotificationWorkItem> queueEntry = null;
            try {
                queueEntry = _queue.Dequeue();
            } catch (Exception ex) {
                if (!(ex is TimeoutException))
                    return JobResult.FromException(ex, "An error occurred while trying to dequeue the next EventNotification: {0}", ex.Message);
            }
            if (queueEntry == null)
                return JobResult.Success;

            var eventModel = _eventRepository.GetById(queueEntry.Value.EventId);
            if (eventModel == null) {
                queueEntry.Abandon();
                return JobResult.FailedWithMessage("Could not load event {0}.", queueEntry.Value.EventId);
            }

            var eventNotification = new EventNotification(queueEntry.Value, eventModel);
            bool shouldLog = eventNotification.Event.ProjectId != Settings.Current.InternalProjectId;
            int emailsSent = 0;
            Log.Trace().Message("Process notification: project={0} event={1} stack={2}", eventNotification.Event.ProjectId, eventNotification.Event.Id, eventNotification.Event.StackId).WriteIf(shouldLog);

            var project = _projectRepository.GetById(eventNotification.Event.ProjectId, true);
            if (project == null) {
                queueEntry.Abandon();
                return JobResult.FailedWithMessage("Could not load project {0}.", eventNotification.Event.ProjectId);
            }
            Log.Trace().Message("Loaded project: name={0}", project.Name).WriteIf(shouldLog);

            var organization = _organizationRepository.GetById(project.OrganizationId, true);
            if (organization == null) {
                queueEntry.Abandon();
                return JobResult.FailedWithMessage("Could not load organization {0}.", project.OrganizationId);
            }
            Log.Trace().Message("Loaded organization: name={0}", organization.Name).WriteIf(shouldLog);

            var stack = _stackRepository.GetById(eventNotification.Event.StackId);
            if (stack == null) {
                queueEntry.Abandon();
                return JobResult.FailedWithMessage("Could not load stack {0}.", eventNotification.Event.StackId);
            }

            if (!organization.HasPremiumFeatures) {
                queueEntry.Complete();
                Log.Info().Message("Skipping \"{0}\" because organization \"{1}\" does not have premium features.", eventNotification.Event.Id, eventNotification.Event.OrganizationId).WriteIf(shouldLog);
                return JobResult.Success;
            }

            if (stack.DisableNotifications || stack.IsHidden) {
                queueEntry.Complete();
                Log.Info().Message("Skipping \"{0}\" because stack \"{1}\" notifications are disabled or stack is hidden.", eventNotification.Event.Id, eventNotification.Event.StackId).WriteIf(shouldLog);
                return JobResult.Success;
            }

            if (token.IsCancellationRequested) {
                queueEntry.Abandon();
                return JobResult.Cancelled;
            }

            Log.Trace().Message("Loaded stack: title={0}", stack.Title).WriteIf(shouldLog);
            int totalOccurrences = stack.TotalOccurrences;

            // after the first 2 occurrences, don't send a notification for the same stack more then once every 30 minutes
            var lastTimeSent = _cacheClient.Get<DateTime>(String.Concat("notify:stack-throttle:", eventNotification.Event.StackId));
            if (totalOccurrences > 2
                && !eventNotification.IsRegression
                && lastTimeSent != DateTime.MinValue
                && lastTimeSent > DateTime.Now.AddMinutes(-30)) {
                queueEntry.Complete();
                Log.Info().Message("Skipping message because of stack throttling: last sent={0} occurrences={1}", lastTimeSent, totalOccurrences).WriteIf(shouldLog);
                return JobResult.Success;
            }

            // don't send more than 10 notifications for a given project every 30 minutes
            var projectTimeWindow = TimeSpan.FromMinutes(30);
            string cacheKey = String.Concat("notify:project-throttle:", eventNotification.Event.ProjectId, "-", DateTime.UtcNow.Floor(projectTimeWindow).Ticks);
            long notificationCount = _cacheClient.Increment(cacheKey, 1, projectTimeWindow);
            if (notificationCount > 10 && !eventNotification.IsRegression) {
                queueEntry.Complete();
                Log.Info().Project(eventNotification.Event.ProjectId).Message("Skipping message because of project throttling: count={0}", notificationCount).WriteIf(shouldLog);
                return JobResult.Success;
            }

            if (token.IsCancellationRequested) {
                queueEntry.Abandon();
                return JobResult.Cancelled;
            }

            foreach (var kv in project.NotificationSettings) {
                var settings = kv.Value;
                Log.Trace().Message("Processing notification: user={0}", kv.Key).WriteIf(shouldLog);

                var user = _userRepository.GetById(kv.Key);
                if (user == null || String.IsNullOrEmpty(user.EmailAddress)) {
                    Log.Error().Message("Could not load user {0} or blank email address {1}.", kv.Key, user != null ? user.EmailAddress : "").Write();
                    continue;
                }

                if (!user.IsEmailAddressVerified) {
                    Log.Info().Message("User {0} with email address {1} has not been verified.", kv.Key, user != null ? user.EmailAddress : "").WriteIf(shouldLog);
                    continue;
                }

                if (!user.EmailNotificationsEnabled) {
                    Log.Info().Message("User {0} with email address {1} has email notifications disabled.", kv.Key, user != null ? user.EmailAddress : "").WriteIf(shouldLog);
                    continue;
                }

                if (!user.OrganizationIds.Contains(project.OrganizationId)) {
                    Log.Error().Message("Unauthorized user: project={0} user={1} organization={2} event={3}", project.Id, kv.Key, project.OrganizationId, eventNotification.Event.Id).Write();
                    continue;
                }

                Log.Trace().Message("Loaded user: email={0}", user.EmailAddress).WriteIf(shouldLog);

                bool shouldReportNewError = settings.ReportNewErrors && eventNotification.IsNew && eventNotification.Event.IsError();
                bool shouldReportCriticalError = settings.ReportCriticalErrors && eventNotification.IsCritical && eventNotification.Event.IsError();
                bool shouldReportRegression = settings.ReportEventRegressions && eventNotification.IsRegression;
                bool shouldReportNewEvent = settings.ReportNewEvents && eventNotification.IsNew;
                bool shouldReportCriticalEvent = settings.ReportCriticalEvents && eventNotification.IsCritical;
                bool shouldReport = shouldReportNewError || shouldReportCriticalError || shouldReportRegression || shouldReportNewEvent || shouldReportCriticalEvent;

                Log.Trace().Message("Settings: newerror={0} criticalerror={1} regression={2} new={3} critical={4}",
                    settings.ReportNewErrors, settings.ReportCriticalErrors,
                    settings.ReportEventRegressions, settings.ReportNewEvents, settings.ReportCriticalEvents).WriteIf(shouldLog);
                Log.Trace().Message("Should process: newerror={0} criticalerror={1} regression={2} new={3} critical={4}",
                    shouldReportNewError, shouldReportCriticalError,
                    shouldReportRegression, shouldReportNewEvent, shouldReportCriticalEvent).WriteIf(shouldLog);

                var requestInfo = eventNotification.Event.GetRequestInfo();
                // check for known bots if the user has elected to not report them
                if (shouldReport && requestInfo != null && !String.IsNullOrEmpty(requestInfo.UserAgent)) {
                    ClientInfo info = null;
                    try {
                        info = Parser.GetDefault().Parse(requestInfo.UserAgent);
                    } catch (Exception ex) {
                        Log.Warn().Project(eventNotification.Event.ProjectId).Message("Unable to parse user agent {0}. Exception: {1}",
                            requestInfo.UserAgent, ex.Message).Write();
                    }

                    var botPatterns = project.Configuration.Settings.ContainsKey(SettingsDictionary.KnownKeys.UserAgentBotPatterns)
                        ? project.Configuration.Settings.GetStringCollection(SettingsDictionary.KnownKeys.UserAgentBotPatterns).ToList()
                        : new List<string>();

                    if (info != null && info.Device.IsSpider || requestInfo.UserAgent.AnyWildcardMatches(botPatterns)) {
                        shouldReport = false;
                        Log.Info().Message("Skipping because event is from a bot \"{0}\".", requestInfo.UserAgent).WriteIf(shouldLog);
                    }
                }

                if (!shouldReport)
                    continue;

                var model = new EventNotificationModel(eventNotification) {
                    ProjectName = project.Name,
                    TotalOccurrences = totalOccurrences
                };

                // don't send notifications in non-production mode to email addresses that are not on the outbound email list.
                if (Settings.Current.WebsiteMode != WebsiteMode.Production
                    && !Settings.Current.AllowedOutboundAddresses.Contains(v => user.EmailAddress.ToLowerInvariant().Contains(v))) {
                        Log.Info().Message("Skipping because email is not on the outbound list and not in production mode.").WriteIf(shouldLog);
                    continue;
                }
                
                Log.Trace().Message("Sending email to {0}...", user.EmailAddress).Write();
                _mailer.SendNotice(user.EmailAddress, model);
                emailsSent++;
                Log.Trace().Message("Done sending email.").WriteIf(shouldLog);
            }

            // if we sent any emails, mark the last time a notification for this stack was sent.
            if (emailsSent > 0) {
                _cacheClient.Set(String.Concat("notify:stack-throttle:", eventNotification.Event.StackId), DateTime.Now, DateTime.Now.AddMinutes(15));
                Log.Info().Message("Notifications sent: event={0} stack={1} count={2}", eventNotification.Event.Id, eventNotification.Event.StackId, emailsSent).WriteIf(shouldLog);
            }

            queueEntry.Complete();

            return JobResult.Success;
        }
    }
}