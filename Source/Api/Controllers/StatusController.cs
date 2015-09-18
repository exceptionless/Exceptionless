using System;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Exceptionless.Api.Models;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
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
        [ResponseType(typeof(StatusResult))]
        public async Task<IHttpActionResult> Index() {
            var result = await _healthChecker.CheckAllAsync().AnyContext();
            if (!result.IsHealthy)
                return StatusCodeWithMessage(HttpStatusCode.ServiceUnavailable, result.Message, result.Message);

            return Ok(new StatusResult { Message = "All Systems Check", Version = Settings.Current.Version });
        }

        [HttpGet]
        [Route("queue-stats")]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult QueueStats() {
            return Ok(new {
                EventPosts = new {
                    Active = _eventQueue.GetQueueCount(),
                    Deadletter = _eventQueue.GetDeadletterCount(),
                    Working = _eventQueue.GetWorkingCount()
                },
                MailMessages = new {
                    Active = _mailQueue.GetQueueCount(),
                    Deadletter = _mailQueue.GetDeadletterCount(),
                    Working = _mailQueue.GetWorkingCount()
                },
                UserDescriptions = new {
                    Active = _userDescriptionQueue.GetQueueCount(),
                    Deadletter = _userDescriptionQueue.GetDeadletterCount(),
                    Working = _userDescriptionQueue.GetWorkingCount()
                },
                Notifications = new {
                    Active = _notificationQueue.GetQueueCount(),
                    Deadletter = _notificationQueue.GetDeadletterCount(),
                    Working = _notificationQueue.GetWorkingCount()
                },
                WebHooks = new {
                    Active = _webHooksQueue.GetQueueCount(),
                    Deadletter = _webHooksQueue.GetDeadletterCount(),
                    Working = _webHooksQueue.GetWorkingCount()
                }
            });
        }

        [HttpGet]
        [Route("metric-stats")]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult MetricStats() {
            var metricsClient = _metricsClient as InMemoryMetricsClient;
            if (metricsClient == null)
                return Ok();

            return Ok(metricsClient.GetMetricStats());
        }

        [HttpPost]
        [Route("notifications/release")]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult PostReleaseNotification([NakedBody]string message = null, bool critical = false) {
            var notification = new ReleaseNotification { Critical = critical, Date = DateTimeOffset.UtcNow, Message = message };
            _messagePublisher.Publish(notification);
            return Ok(notification);
        }

        /// <summary>
        /// Returns the current system notification messages.
        /// </summary>
        [HttpGet]
        [Route("notifications/system")]
        [ResponseType(typeof(SystemNotification))]
        public IHttpActionResult GetSystemNotification() {
            var notification = _cacheClient.Get<SystemNotification>("system-notification");
            if (notification == null)
                return Ok();

            return Ok(notification);
        }

        [HttpPost]
        [Route("notifications/system")]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult PostSystemNotification([NakedBody]string message) {
            if (String.IsNullOrEmpty(message))
                return NotFound();

            var notification = new SystemNotification { Date = DateTimeOffset.UtcNow, Message = message };
            _cacheClient.Set("system-notification", notification);
            _messagePublisher.Publish(notification);

            return Ok(notification);
        }

        [HttpDelete]
        [Route("notifications/system")]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult RemoveSystemNotification() {
            _cacheClient.Remove("system-notification");
            _messagePublisher.Publish(new SystemNotification { Date = DateTimeOffset.UtcNow });
            return Ok();
        }
    }
}
