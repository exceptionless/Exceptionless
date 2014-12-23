using System;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Models;
using NLog.Fluent;
using UAParser;

namespace Exceptionless.Core.Jobs {
    public class EventNotificationsJob : JobBase {
        private readonly IQueue<EventNotification> _queue;
        private readonly IMailer _mailer;
        private readonly IAppStatsClient _statsClient;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICacheClient _cacheClient;

        public EventNotificationsJob(IQueue<EventNotification> queue, IMailer mailer, IAppStatsClient statsClient,
            IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IStackRepository stackRepository,
            IUserRepository userRepository, ICacheClient cacheClient) {
            _queue = queue;
            _mailer = mailer;
            _statsClient = statsClient;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
            _userRepository = userRepository;
            _cacheClient = cacheClient;
        }

        protected async override Task<JobResult> RunInternalAsync(CancellationToken token) {
            Log.Trace().Message("Process event notification job starting").Write();

            QueueEntry<EventNotification> queueEntry = null;
            try {
                queueEntry = _queue.Dequeue();
            } catch (Exception ex) {
                if (!(ex is TimeoutException))
                    return JobResult.FromException(ex, "An error occurred while trying to dequeue the next EventNotification: {0}", ex.Message);
            }
            if (queueEntry == null)
                return JobResult.Success;

            var eventNotification = queueEntry.Value;
            int emailsSent = 0;
            Log.Trace().Message("Process notification: project={0} event={1} stack={2}", eventNotification.Event.ProjectId, eventNotification.Event.Id, eventNotification.Event.StackId).Write();

            var project = _projectRepository.GetById(eventNotification.Event.ProjectId, true);
            if (project == null) {
                queueEntry.Abandon();
                return JobResult.FailedWithMessage("Could not load project {0}.", eventNotification.Event.ProjectId);
            }
            Log.Trace().Message("Loaded project: name={0}", project.Name).Write();

            var organization = _organizationRepository.GetById(project.OrganizationId, true);
            if (organization == null) {
                queueEntry.Abandon();
                return JobResult.FailedWithMessage("Could not load organization {0}.", project.OrganizationId);
            }
            Log.Trace().Message("Loaded organization: name={0}", organization.Name).Write();

            var stack = _stackRepository.GetById(eventNotification.Event.StackId);
            if (stack == null) {
                queueEntry.Abandon();
                return JobResult.FailedWithMessage("Could not load stack {0}.", eventNotification.Event.StackId);
            }

            if (!organization.HasPremiumFeatures) {
                queueEntry.Complete();
                Log.Trace().Message("Skipping because organization does not have premium features.").Write();
                return JobResult.Success;
            }

            if (stack.DisableNotifications || stack.IsHidden) {
                queueEntry.Complete();
                Log.Trace().Message("Skipping because stack notifications are disabled or stack is hidden.").Write();
                return JobResult.Success;
            }

            Log.Trace().Message("Loaded stack: title={0}", stack.Title).Write();
            int totalOccurrences = stack.TotalOccurrences;

            // after the first 2 occurrences, don't send a notification for the same stack more then once every 30 minutes
            var lastTimeSent = _cacheClient.Get<DateTime>(String.Concat("notify:stack-throttle:", eventNotification.Event.StackId));
            if (totalOccurrences > 2
                && !eventNotification.IsRegression
                && lastTimeSent != DateTime.MinValue
                && lastTimeSent > DateTime.Now.AddMinutes(-30)) {
                queueEntry.Complete();
                Log.Info().Message("Skipping message because of stack throttling: last sent={0} occurrences={1}", lastTimeSent, totalOccurrences).Write();
                return JobResult.Success;
            }

            // don't send more than 10 notifications for a given project every 30 minutes
            var projectTimeWindow = TimeSpan.FromMinutes(30);
            string cacheKey = String.Concat("notify:project-throttle:", eventNotification.Event.ProjectId, "-", DateTime.UtcNow.Floor(projectTimeWindow).Ticks);
            long notificationCount = _cacheClient.Increment(cacheKey, 1, projectTimeWindow);
            if (notificationCount > 10 && !eventNotification.IsRegression) {
                queueEntry.Complete();
                Log.Info().Project(eventNotification.Event.ProjectId).Message("Skipping message because of project throttling: count={0}", notificationCount).Write();
                return JobResult.Success;
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
                        project.OrganizationId, eventNotification.Event.Id).Write();
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
                if (shouldReportOccurrence && settings.Report404Errors == false && eventNotification.Event.IsNotFound()) {
                    shouldReportOccurrence = false;
                    Log.Trace().Message("Skipping because message is 404.").Write();
                }

                var requestInfo = eventNotification.Event.GetRequestInfo();
                // check for known bots if the user has elected to not report them
                if (shouldReportOccurrence && settings.ReportKnownBotErrors == false &&
                    requestInfo != null && !String.IsNullOrEmpty(requestInfo.UserAgent)) {
                    ClientInfo info = null;
                    try {
                        info = Parser.GetDefault().Parse(requestInfo.UserAgent);
                    } catch (Exception ex) {
                        Log.Warn().Project(eventNotification.Event.ProjectId).Message("Unable to parse user agent {0}. Exception: {1}",
                            requestInfo.UserAgent, ex.Message).Write();
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
            if (emailsSent > 0) {
                _cacheClient.Set(String.Concat("notify:stack-throttle:", eventNotification.Event.StackId), DateTime.Now, DateTime.Now.AddMinutes(15));
                Log.Info().Message("Notifications sent: event={0} stack={1} count={2}", eventNotification.Event.Id, eventNotification.Event.StackId, emailsSent).Write();
            }

            queueEntry.Complete();

            return JobResult.Success;
        }
    }
}