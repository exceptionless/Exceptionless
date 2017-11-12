using System;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Exceptionless.Api.Controllers {
    [Route(API_PREFIX)]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public class StatusController : ExceptionlessApiController {
        private readonly ICacheClient _cacheClient;
        private readonly IMessagePublisher _messagePublisher;
        private readonly SystemHealthChecker _healthChecker;
        private readonly IQueue<EventPost> _eventQueue;
        private readonly IQueue<MailMessage> _mailQueue;
        private readonly IQueue<EventNotificationWorkItem> _notificationQueue;
        private readonly IQueue<WebHookNotification> _webHooksQueue;
        private readonly IQueue<EventUserDescription> _userDescriptionQueue;

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
        }

        /// <summary>
        /// Get the status of the API
        /// </summary>
        /// <response code="503">Contains a message detailing the service outage message.</response>
        [AllowAnonymous]
        [HttpGet("status")]
        public async Task<IActionResult> IndexAsync() {
            if (_lastHealthCheckResult == null || _nextHealthCheckTimeUtc < SystemClock.UtcNow) {
                _nextHealthCheckTimeUtc = SystemClock.UtcNow.AddSeconds(5);
                _lastHealthCheckResult = await _healthChecker.CheckAllAsync();
            }

            if (!_lastHealthCheckResult.IsHealthy)
                return StatusCodeWithMessage(StatusCodes.Status503ServiceUnavailable, _lastHealthCheckResult.Message, _lastHealthCheckResult.Message);

            if (Settings.Current.HasAppScope) {
                return Ok(new {
                    Message = "All Systems Check",
                    Settings.Current.InformationalVersion,
                    Settings.Current.AppScope,
                    AppMode = Settings.Current.AppMode.ToString(),
                    Environment.MachineName
                });
            }

            return Ok(new {
                Message = "All Systems Check",
                Settings.Current.InformationalVersion,
                AppMode = Settings.Current.AppMode.ToString(),
                Environment.MachineName
            });
        }

        [HttpGet("queue-stats")]
        [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
        public async Task<IActionResult> QueueStatsAsync() {
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

        [HttpPost("notifications/release")]
        [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
        public async Task<IActionResult> PostReleaseNotificationAsync([FromBody] string message = null, [FromQuery] bool critical = false) {
            var notification = new ReleaseNotification { Critical = critical, Date = SystemClock.UtcNow, Message = message };
            await _messagePublisher.PublishAsync(notification);
            return Ok(notification);
        }

        /// <summary>
        /// Returns the current system notification messages.
        /// </summary>
        [HttpGet("notifications/system")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(SystemNotification))]
        public async Task<IActionResult> GetSystemNotificationAsync() {
            var notification = await _cacheClient.GetAsync<SystemNotification>("system-notification");
            if (!notification.HasValue)
                return Ok();

            return Ok(notification.Value);
        }

        [HttpPost("notifications/system")]
        [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
        public async Task<IActionResult> PostSystemNotificationAsync([FromBody] string message) {
            if (String.IsNullOrWhiteSpace(message))
                return NotFound();

            var notification = new SystemNotification { Date = SystemClock.UtcNow, Message = message };
            await _cacheClient.SetAsync("system-notification", notification);
            await _messagePublisher.PublishAsync(notification);

            return Ok(notification);
        }

        [HttpDelete("notifications/system")]
        [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
        public async Task<IActionResult> RemoveSystemNotificationAsync() {
            await _cacheClient.RemoveAsync("system-notification");
            await _messagePublisher.PublishAsync(new SystemNotification { Date = SystemClock.UtcNow });
            return Ok();
        }
    }
}
