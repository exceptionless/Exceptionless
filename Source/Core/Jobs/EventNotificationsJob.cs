using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Queues;

#pragma warning disable 1998

namespace Exceptionless.Core.Jobs {
    public class EventNotificationsJob : QueueJobBase<EventNotificationWorkItem> {
        private readonly IMailer _mailer;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IUserRepository _userRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ICacheClient _cacheClient;
        private readonly UserAgentParser _parser;

        public EventNotificationsJob(IQueue<EventNotificationWorkItem> queue, IMailer mailer, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IStackRepository stackRepository, IUserRepository userRepository, IEventRepository eventRepository, ICacheClient cacheClient, UserAgentParser parser, ILoggerFactory loggerFactory = null) : base(queue, loggerFactory) {
            _mailer = mailer;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
            _userRepository = userRepository;
            _eventRepository = eventRepository;
            _cacheClient = cacheClient;
            _parser = parser;
        }

        protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<EventNotificationWorkItem> context) {
            var eventModel = await _eventRepository.GetByIdAsync(context.QueueEntry.Value.EventId).AnyContext();
            if (eventModel == null)
                return JobResult.FailedWithMessage($"Could not load event: {context.QueueEntry.Value.EventId}");

            var eventNotification = new EventNotification(context.QueueEntry.Value, eventModel);
            bool shouldLog = eventNotification.Event.ProjectId != Settings.Current.InternalProjectId;
            int emailsSent = 0;
            _logger.Trace().Message("Process notification: project={0} event={1} stack={2}", eventNotification.Event.ProjectId, eventNotification.Event.Id, eventNotification.Event.StackId).WriteIf(shouldLog);

            var project = await _projectRepository.GetByIdAsync(eventNotification.Event.ProjectId, true).AnyContext();
            if (project == null)
                return JobResult.FailedWithMessage($"Could not load project: {eventNotification.Event.ProjectId}.");
            _logger.Trace().Message($"Loaded project: name={project.Name}").WriteIf(shouldLog);

            var organization = await _organizationRepository.GetByIdAsync(project.OrganizationId, true).AnyContext();
            if (organization == null)
                return JobResult.FailedWithMessage($"Could not load organization: {project.OrganizationId}");

            _logger.Trace().Message($"Loaded organization: {organization.Name}").WriteIf(shouldLog);

            var stack = await _stackRepository.GetByIdAsync(eventNotification.Event.StackId).AnyContext();
            if (stack == null)
                return JobResult.FailedWithMessage($"Could not load stack: {eventNotification.Event.StackId}");

            if (!organization.HasPremiumFeatures) {
                _logger.Info().Message("Skipping \"{0}\" because organization \"{1}\" does not have premium features.", eventNotification.Event.Id, eventNotification.Event.OrganizationId).WriteIf(shouldLog);
                return JobResult.Success;
            }

            if (stack.DisableNotifications || stack.IsHidden) {
                _logger.Info().Message("Skipping \"{0}\" because stack \"{1}\" notifications are disabled or stack is hidden.", eventNotification.Event.Id, eventNotification.Event.StackId).WriteIf(shouldLog);
                return JobResult.Success;
            }

            if (context.CancellationToken.IsCancellationRequested)
                return JobResult.Cancelled;

            _logger.Trace().Message("Loaded stack: title={0}", stack.Title).WriteIf(shouldLog);
            int totalOccurrences = stack.TotalOccurrences;

            // after the first 2 occurrences, don't send a notification for the same stack more then once every 30 minutes
            var lastTimeSentUtc = await _cacheClient.GetAsync<DateTime>(String.Concat("notify:stack-throttle:", eventNotification.Event.StackId), DateTime.MinValue).AnyContext();
            if (totalOccurrences > 2
                && !eventNotification.IsRegression
                && lastTimeSentUtc != DateTime.MinValue
                && lastTimeSentUtc > DateTime.UtcNow.AddMinutes(-30)) {
                _logger.Info().Message("Skipping message because of stack throttling: last sent={0} occurrences={1}", lastTimeSentUtc, totalOccurrences).WriteIf(shouldLog);
                return JobResult.Success;
            }

            // don't send more than 10 notifications for a given project every 30 minutes
            var projectTimeWindow = TimeSpan.FromMinutes(30);
            string cacheKey = String.Concat("notify:project-throttle:", eventNotification.Event.ProjectId, "-", DateTime.UtcNow.Floor(projectTimeWindow).Ticks);
            double notificationCount = await _cacheClient.IncrementAsync(cacheKey, 1, projectTimeWindow).AnyContext();
            if (notificationCount > 10 && !eventNotification.IsRegression) {
                _logger.Info().Project(eventNotification.Event.ProjectId).Message("Skipping message because of project throttling: count={0}", notificationCount).WriteIf(shouldLog);
                return JobResult.Success;
            }

            if (context.CancellationToken.IsCancellationRequested)
                return JobResult.Cancelled;

            foreach (var kv in project.NotificationSettings) {
                var settings = kv.Value;
                _logger.Trace().Message("Processing notification: user={0}", kv.Key).WriteIf(shouldLog);

                var user = await _userRepository.GetByIdAsync(kv.Key).AnyContext();
                if (String.IsNullOrEmpty(user?.EmailAddress)) {
                    _logger.Error("Could not load user {0} or blank email address {1}.", kv.Key, user?.EmailAddress ?? "");
                    continue;
                }

                if (!user.IsEmailAddressVerified) {
                    _logger.Info().Message("User {0} with email address {1} has not been verified.", user.Id, user.EmailAddress).WriteIf(shouldLog);
                    continue;
                }

                if (!user.EmailNotificationsEnabled) {
                    _logger.Info().Message("User {0} with email address {1} has email notifications disabled.", user.Id, user.EmailAddress).WriteIf(shouldLog);
                    continue;
                }

                if (!user.OrganizationIds.Contains(project.OrganizationId)) {
                    _logger.Error().Message("Unauthorized user: project={0} user={1} organization={2} event={3}", project.Id, kv.Key, project.OrganizationId, eventNotification.Event.Id).Write();
                    continue;
                }

                _logger.Trace().Message("Loaded user: email={0}", user.EmailAddress).WriteIf(shouldLog);

                bool shouldReportNewError = settings.ReportNewErrors && eventNotification.IsNew && eventNotification.Event.IsError();
                bool shouldReportCriticalError = settings.ReportCriticalErrors && eventNotification.IsCritical && eventNotification.Event.IsError();
                bool shouldReportRegression = settings.ReportEventRegressions && eventNotification.IsRegression;
                bool shouldReportNewEvent = settings.ReportNewEvents && eventNotification.IsNew;
                bool shouldReportCriticalEvent = settings.ReportCriticalEvents && eventNotification.IsCritical;
                bool shouldReport = shouldReportNewError || shouldReportCriticalError || shouldReportRegression || shouldReportNewEvent || shouldReportCriticalEvent;

                _logger.Trace().Message("Settings: newerror={0} criticalerror={1} regression={2} new={3} critical={4}",
                    settings.ReportNewErrors, settings.ReportCriticalErrors,
                    settings.ReportEventRegressions, settings.ReportNewEvents, settings.ReportCriticalEvents).WriteIf(shouldLog);
                _logger.Trace().Message("Should process: newerror={0} criticalerror={1} regression={2} new={3} critical={4}",
                    shouldReportNewError, shouldReportCriticalError,
                    shouldReportRegression, shouldReportNewEvent, shouldReportCriticalEvent).WriteIf(shouldLog);

                var request = eventNotification.Event.GetRequestInfo();
                // check for known bots if the user has elected to not report them
                if (shouldReport && !String.IsNullOrEmpty(request?.UserAgent)) {
                    var botPatterns = project.Configuration.Settings.ContainsKey(SettingsDictionary.KnownKeys.UserAgentBotPatterns)
                        ? project.Configuration.Settings.GetStringCollection(SettingsDictionary.KnownKeys.UserAgentBotPatterns).ToList()
                        : new List<string>();

                    var info = await _parser.ParseAsync(request.UserAgent, eventNotification.Event.ProjectId).AnyContext();
                    if (info != null && info.Device.IsSpider || request.UserAgent.AnyWildcardMatches(botPatterns)) {
                        shouldReport = false;
                        _logger.Info().Message("Skipping because event is from a bot \"{0}\".", request.UserAgent).WriteIf(shouldLog);
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
                    _logger.Info().Message("Skipping because email is not on the outbound list and not in production mode.").WriteIf(shouldLog);
                    continue;
                }

                _logger.Trace("Sending email to {0}...", user.EmailAddress);
                await _mailer.SendEventNoticeAsync(user.EmailAddress, model).AnyContext();
                emailsSent++;
                _logger.Trace().Message("Done sending email.").WriteIf(shouldLog);
            }

            // if we sent any emails, mark the last time a notification for this stack was sent.
            if (emailsSent > 0) {
                await _cacheClient.SetAsync(String.Concat("notify:stack-throttle:", eventNotification.Event.StackId), DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15)).AnyContext();
                _logger.Info().Message("Notifications sent: event={0} stack={1} count={2}", eventNotification.Event.Id, eventNotification.Event.StackId, emailsSent).WriteIf(shouldLog);
            }

            return JobResult.Success;
        }
    }
}
