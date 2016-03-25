using System;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Queues;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class StatusController : ExceptionlessApiController {
        private readonly ICacheClient _cacheClient;
        private readonly IMessagePublisher _messagePublisher;
        private readonly SystemHealthChecker _healthChecker;
        private readonly IQueue<EventPost> _eventQueue;
        private readonly IQueue<MailMessage> _mailQueue;
        private readonly IQueue<EventNotificationWorkItem> _notificationQueue;
        private readonly IQueue<WebHookNotification> _webHooksQueue;
        private readonly IQueue<EventUserDescription> _userDescriptionQueue;
        private readonly IMetricsClient _metricsClient;

        private static HealthCheckResult _lastHealthCheckResult;
        private static DateTime _nextHealthCheckTimeUtc = DateTime.MinValue;

        public StatusController(ICacheClient cacheClient, IMessagePublisher messagePublisher, SystemHealthChecker healthChecker, IQueue<EventPost> eventQueue, IQueue<MailMessage> mailQueue, IQueue<EventNotificationWorkItem> notificationQueue, IQueue<WebHookNotification> webHooksQueue, IQueue<EventUserDescription> userDescriptionQueue, IMetricsClient metricsClient) {
            _cacheClient = cacheClient;
            _messagePublisher = messagePublisher;
            _healthChecker = healthChecker;
            _eventQueue = eventQueue;
            _mailQueue = mailQueue;
            _notificationQueue = notificationQueue;
            _webHooksQueue = webHooksQueue;
            _userDescriptionQueue = userDescriptionQueue;
            _metricsClient = metricsClient;
        }

        /// <summary>
        /// Get the status of the API
        /// </summary>
        /// <response code="503">Contains a message detailing the service outage message.</response>
        [HttpGet]
        [Route("status")]
        public async Task<IHttpActionResult> IndexAsync() {
            if (_lastHealthCheckResult == null || _nextHealthCheckTimeUtc < DateTime.UtcNow) {
                _nextHealthCheckTimeUtc = DateTime.UtcNow.AddSeconds(5);
                _lastHealthCheckResult = await _healthChecker.CheckAllAsync();
            }

            if (!_lastHealthCheckResult.IsHealthy)
                return StatusCodeWithMessage(HttpStatusCode.ServiceUnavailable, _lastHealthCheckResult.Message, _lastHealthCheckResult.Message);

            if (Settings.Current.HasAppScope) {
                return Ok(new {
                    Message = "All Systems Check",
                    Settings.Current.Version,
                    Settings.Current.AppScope,
                    WebsiteMode = Settings.Current.WebsiteMode.ToString(),
                    Environment.MachineName
                });
            }

            return Ok(new {
                Message = "All Systems Check",
                Settings.Current.Version,
                WebsiteMode = Settings.Current.WebsiteMode.ToString(),
                Environment.MachineName
            });
        }

        [HttpGet]
        [Route("queue-stats")]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public async Task<IHttpActionResult> QueueStatsAsync() {
            var eventQueueStats = await _eventQueue.GetQueueStatsAsync();
            var mailQueueStats = await _mailQueue.GetQueueStatsAsync();
            var userDescriptionQueueStats = await _userDescriptionQueue.GetQueueStatsAsync();
            var notificationQueueStats = await _notificationQueue.GetQueueStatsAsync();
            var webHooksQueueStats = await _webHooksQueue.GetQueueStatsAsync();

            return Ok(new {
                EventPosts = new {
                    Active = eventQueueStats.Enqueued,
                    Deadletter = eventQueueStats.Deadletter,
                    Working = eventQueueStats.Working
                },
                MailMessages = new {
                    Active = mailQueueStats.Enqueued,
                    Deadletter = mailQueueStats.Deadletter,
                    Working = mailQueueStats.Working
                },
                UserDescriptions = new {
                    Active = userDescriptionQueueStats.Enqueued,
                    Deadletter = userDescriptionQueueStats.Deadletter,
                    Working = userDescriptionQueueStats.Working
                },
                Notifications = new {
                    Active = notificationQueueStats.Enqueued,
                    Deadletter = notificationQueueStats.Deadletter,
                    Working = notificationQueueStats.Working
                },
                WebHooks = new {
                    Active = webHooksQueueStats.Enqueued,
                    Deadletter = webHooksQueueStats.Deadletter,
                    Working = webHooksQueueStats.Working
                }
            });
        }
        
        [HttpPost]
        [Route("notifications/release")]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public async Task<IHttpActionResult> PostReleaseNotificationAsync([NakedBody]string message = null, bool critical = false) {
            var notification = new ReleaseNotification { Critical = critical, Date = DateTimeOffset.UtcNow, Message = message };
            await _messagePublisher.PublishAsync(notification);
            return Ok(notification);
        }

        /// <summary>
        /// Returns the current system notification messages.
        /// </summary>
        [HttpGet]
        [Route("notifications/system")]
        [ResponseType(typeof(SystemNotification))]
        public async Task<IHttpActionResult> GetSystemNotificationAsync() {
            var notification = await _cacheClient.GetAsync<SystemNotification>("system-notification");
            if (notification == null)
                return Ok();

            return Ok(notification);
        }

        [HttpPost]
        [Route("notifications/system")]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public async Task<IHttpActionResult> PostSystemNotificationAsync([NakedBody]string message) {
            if (String.IsNullOrEmpty(message))
                return NotFound();

            var notification = new SystemNotification { Date = DateTimeOffset.UtcNow, Message = message };
            await _cacheClient.SetAsync("system-notification", notification);
            await _messagePublisher.PublishAsync(notification);

            return Ok(notification);
        }

        [HttpDelete]
        [Route("notifications/system")]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public async Task<IHttpActionResult> RemoveSystemNotificationAsync() {
            await _cacheClient.RemoveAsync("system-notification");
            await _messagePublisher.PublishAsync(new SystemNotification { Date = DateTimeOffset.UtcNow });
            return Ok();
        }
    }
}
