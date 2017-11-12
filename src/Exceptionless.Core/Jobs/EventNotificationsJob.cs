using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

#pragma warning disable 1998

namespace Exceptionless.Core.Jobs {
    [Job(Description = "Queues event notification emails.", InitialDelay = "5s")]
    public class EventNotificationsJob : QueueJobBase<EventNotificationWorkItem> {
        private readonly SlackService _slackService;
        private readonly IMailer _mailer;
        private readonly IProjectRepository _projectRepository;
        private readonly IUserRepository _userRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ICacheClient _cache;
        private readonly UserAgentParser _parser;

        public EventNotificationsJob(IQueue<EventNotificationWorkItem> queue, SlackService slackService, IMailer mailer, IProjectRepository projectRepository, IUserRepository userRepository, IEventRepository eventRepository, ICacheClient cacheClient, UserAgentParser parser, ILoggerFactory loggerFactory = null) : base(queue, loggerFactory) {
            _slackService = slackService;
            _mailer = mailer;
            _projectRepository = projectRepository;
            _userRepository = userRepository;
            _eventRepository = eventRepository;
            _cache = cacheClient;
            _parser = parser;
        }

        protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<EventNotificationWorkItem> context) {
            var wi = context.QueueEntry.Value;
            var ev = await _eventRepository.GetByIdAsync(wi.EventId).AnyContext();
            if (ev == null || ev.IsDeleted)
                return JobResult.SuccessWithMessage($"Could not load event: {wi.EventId}");

            bool shouldLog = ev.ProjectId != Settings.Current.InternalProjectId;
            int sent = 0;
            if (shouldLog) _logger.LogTrace("Process notification: project={project} event={id} stack={stack}", ev.ProjectId, ev.Id, ev.StackId);

            var project = await _projectRepository.GetByIdAsync(ev.ProjectId, o => o.Cache()).AnyContext();
            if (project == null)
                return JobResult.SuccessWithMessage($"Could not load project: {ev.ProjectId}.");

            using (_logger.BeginScope(new ExceptionlessState().Organization(project.OrganizationId).Project(project.Id))) {
                if (shouldLog) _logger.LogTrace("Loaded project: name={ProjectName}", project.Name);

                // after the first 2 occurrences, don't send a notification for the same stack more then once every 30 minutes
                var lastTimeSentUtc = await _cache.GetAsync<DateTime>(String.Concat("notify:stack-throttle:", ev.StackId), DateTime.MinValue).AnyContext();
                if (wi.TotalOccurrences > 2 && !wi.IsRegression && lastTimeSentUtc != DateTime.MinValue && lastTimeSentUtc > SystemClock.UtcNow.AddMinutes(-30)) {
                    if (shouldLog) _logger.LogInformation("Skipping message because of stack throttling: last sent={LastSentUtc} occurrences={TotalOccurrences}", lastTimeSentUtc, wi.TotalOccurrences);
                    return JobResult.Success;
                }

                if (context.CancellationToken.IsCancellationRequested)
                    return JobResult.Cancelled;

                // don't send more than 10 notifications for a given project every 30 minutes
                var projectTimeWindow = TimeSpan.FromMinutes(30);
                string cacheKey = String.Concat("notify:project-throttle:", ev.ProjectId, "-", SystemClock.UtcNow.Floor(projectTimeWindow).Ticks);
                double notificationCount = await _cache.IncrementAsync(cacheKey, 1, projectTimeWindow).AnyContext();
                if (notificationCount > 10 && !wi.IsRegression) {
                    if (shouldLog) _logger.LogInformation("Skipping message because of project throttling: count={NotificationCount}", notificationCount);
                    return JobResult.Success;
                }

                foreach (var kv in project.NotificationSettings) {
                    var settings = kv.Value;
                    if (shouldLog) _logger.LogTrace("Processing notification: {Key}", kv.Key);

                    bool isCritical = ev.IsCritical();
                    bool shouldReportNewError = settings.ReportNewErrors && wi.IsNew && ev.IsError();
                    bool shouldReportCriticalError = settings.ReportCriticalErrors && isCritical && ev.IsError();
                    bool shouldReportRegression = settings.ReportEventRegressions && wi.IsRegression;
                    bool shouldReportNewEvent = settings.ReportNewEvents && wi.IsNew;
                    bool shouldReportCriticalEvent = settings.ReportCriticalEvents && isCritical;
                    bool shouldReport = shouldReportNewError || shouldReportCriticalError || shouldReportRegression || shouldReportNewEvent || shouldReportCriticalEvent;

                    if (shouldLog) {
                        _logger.LogTrace("Settings: new error={ReportNewErrors} critical error={ReportCriticalErrors} regression={ReportEventRegressions} new={ReportNewEvents} critical={ReportCriticalEvents}", settings.ReportNewErrors, settings.ReportCriticalErrors, settings.ReportEventRegressions, settings.ReportNewEvents, settings.ReportCriticalEvents);
                        _logger.LogTrace("Should process: new error={ShouldReportNewError} critical error={ShouldReportCriticalError} regression={ShouldReportRegression} new={ShouldReportNewEvent} critical={ShouldReportCriticalEvent}", shouldReportNewError, shouldReportCriticalError, shouldReportRegression, shouldReportNewEvent, shouldReportCriticalEvent);
                    }
                    var request = ev.GetRequestInfo();
                    // check for known bots if the user has elected to not report them
                    if (shouldReport && !String.IsNullOrEmpty(request?.UserAgent)) {
                        var botPatterns = project.Configuration.Settings.ContainsKey(SettingsDictionary.KnownKeys.UserAgentBotPatterns) ? project.Configuration.Settings.GetStringCollection(SettingsDictionary.KnownKeys.UserAgentBotPatterns).ToList() : new List<string>();

                        var info = await _parser.ParseAsync(request.UserAgent).AnyContext();
                        if (info != null && info.Device.IsSpider || request.UserAgent.AnyWildcardMatches(botPatterns)) {
                            shouldReport = false;
                            if (shouldLog) _logger.LogInformation("Skipping because event is from a bot {UserAgent}.", request.UserAgent);
                        }
                    }

                    if (!shouldReport)
                        continue;

                    bool processed;
                    switch (kv.Key) {
                        case Project.NotificationIntegrations.Slack:
                            processed = await _slackService.SendEventNoticeAsync(ev, project, wi.IsNew, wi.IsRegression, wi.TotalOccurrences).AnyContext();
                            break;
                        default:
                            processed = await SendEmailNotificationAsync(kv.Key, project, ev, wi, shouldLog).AnyContext();
                            break;
                    }

                    if (shouldLog) _logger.LogTrace("Finished processing notification: {Key}", kv.Key);
                    if (processed)
                        sent++;
                }

                // if we sent any notifications, mark the last time a notification for this stack was sent.
                if (sent > 0) {
                    await _cache.SetAsync(String.Concat("notify:stack-throttle:", ev.StackId), SystemClock.UtcNow, SystemClock.UtcNow.AddMinutes(15)).AnyContext();
                    if (shouldLog) _logger.LogInformation("Notifications sent: event={id} stack={stack} count={SentCount}", ev.Id, ev.StackId, sent);
                }
            }
            return JobResult.Success;
        }

        private async Task<bool> SendEmailNotificationAsync(string userId, Project project, PersistentEvent ev, EventNotificationWorkItem wi, bool shouldLog) {
            var user = await _userRepository.GetByIdAsync(userId, o => o.Cache()).AnyContext();
            if (String.IsNullOrEmpty(user?.EmailAddress)) {
                if (shouldLog) _logger.LogError("Could not load user {user} or blank email address {EmailAddress}.", userId, user?.EmailAddress ?? "");
                return false;
            }

            if (!user.IsEmailAddressVerified) {
                if (shouldLog) _logger.LogInformation("User {user} with email address {EmailAddress} has not been verified.", user.Id, user.EmailAddress);
                return false;
            }

            if (!user.EmailNotificationsEnabled) {
                if (shouldLog) _logger.LogInformation("User {user} with email address {EmailAddress} has email notifications disabled.", user.Id, user.EmailAddress);
                return false;
            }

            if (!user.OrganizationIds.Contains(project.OrganizationId)) {
                if (shouldLog) _logger.LogError("Unauthorized user: project={project} user={user} organization={organization} event={id}", project.Id, userId, project.OrganizationId, ev.Id);
                return false;
            }

            if (shouldLog) _logger.LogTrace("Loaded user: email={EmailAddress}", user.EmailAddress);

            // don't send notifications in non-production mode to email addresses that are not on the outbound email list.
            if (Settings.Current.AppMode != AppMode.Production && !Settings.Current.AllowedOutboundAddresses.Contains(v => user.EmailAddress.ToLowerInvariant().Contains(v))) {
                if (shouldLog) _logger.LogInformation("Skipping because email is not on the outbound list and not in production mode.");
                return false;
            }

            if (shouldLog) _logger.LogTrace("Sending email to {EmailAddress}...", user.EmailAddress);
            return await _mailer.SendEventNoticeAsync(user, ev, project, wi.IsNew, wi.IsRegression, wi.TotalOccurrences).AnyContext();
        }
    }
}
