using System;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Web.Models;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Controllers {
    [Route(API_PREFIX)]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public class StatusController : ExceptionlessApiController {
        private readonly ICacheClient _cacheClient;
        private readonly IMessagePublisher _messagePublisher;
        private readonly IQueue<EventPost> _eventQueue;
        private readonly IQueue<MailMessage> _mailQueue;
        private readonly IQueue<EventNotificationWorkItem> _notificationQueue;
        private readonly IQueue<WebHookNotification> _webHooksQueue;
        private readonly IQueue<EventUserDescription> _userDescriptionQueue;
        private readonly AppOptions _appOptions;

        public StatusController(
            ICacheClient cacheClient,
            IMessagePublisher messagePublisher,
            IQueue<EventPost> eventQueue,
            IQueue<MailMessage> mailQueue,
            IQueue<EventNotificationWorkItem> notificationQueue,
            IQueue<WebHookNotification> webHooksQueue,
            IQueue<EventUserDescription> userDescriptionQueue,
            AppOptions appOptions) {
            _cacheClient = cacheClient;
            _messagePublisher = messagePublisher;
            _eventQueue = eventQueue;
            _mailQueue = mailQueue;
            _notificationQueue = notificationQueue;
            _webHooksQueue = webHooksQueue;
            _userDescriptionQueue = userDescriptionQueue;
            _appOptions = appOptions;
        }

        /// <summary>
        /// Get the info of the API
        /// </summary>
        [AllowAnonymous]
        [HttpGet("about")]
        public IActionResult IndexAsync() {
            return Ok(new {
                _appOptions.InformationalVersion,
                AppMode = _appOptions.AppMode.ToString(),
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
                    eventQueueStats.Deadletter,
                    eventQueueStats.Working
                },
                MailMessages = new {
                    Active = mailQueueStats.Enqueued,
                    mailQueueStats.Deadletter,
                    mailQueueStats.Working
                },
                UserDescriptions = new {
                    Active = userDescriptionQueueStats.Enqueued,
                    userDescriptionQueueStats.Deadletter,
                    userDescriptionQueueStats.Working
                },
                Notifications = new {
                    Active = notificationQueueStats.Enqueued,
                    notificationQueueStats.Deadletter,
                    notificationQueueStats.Working
                },
                WebHooks = new {
                    Active = webHooksQueueStats.Enqueued,
                    webHooksQueueStats.Deadletter,
                    webHooksQueueStats.Working
                }
            });
        }

        [HttpPost("notifications/release")]
        [Consumes("application/json")]
        [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
        public async Task<ActionResult<ReleaseNotification>> PostReleaseNotificationAsync(ValueFromBody<string> message, bool critical = false) {
            var notification = new ReleaseNotification { Critical = critical, Date = SystemClock.UtcNow, Message = message?.Value };
            await _messagePublisher.PublishAsync(notification);
            return Ok(notification);
        }

        /// <summary>
        /// Returns the current system notification messages.
        /// </summary>
        [HttpGet("notifications/system")]
        public async Task<ActionResult<SystemNotification>> GetSystemNotificationAsync() {
            var notification = await _cacheClient.GetAsync<SystemNotification>("system-notification");
            if (!notification.HasValue)
                return Ok();

            return Ok(notification.Value);
        }

        [HttpPost("notifications/system")]
        [Consumes("application/json")]
        [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
        public async Task<ActionResult<SystemNotification>> PostSystemNotificationAsync(ValueFromBody<string> message) {
            if (String.IsNullOrWhiteSpace(message?.Value))
                return NotFound();

            var notification = new SystemNotification { Date = SystemClock.UtcNow, Message = message.Value };
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
