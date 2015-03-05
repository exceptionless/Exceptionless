using System;
using System.Net;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;
using Foundatio.Metrics;
using Foundatio.Queues;

namespace Exceptionless.Api.Controllers {
    public class StatusController : ExceptionlessApiController {
        private readonly SystemHealthChecker _healthChecker;
        private readonly IQueue<EventPost> _eventQueue;
        private readonly IQueue<MailMessage> _mailQueue;
        private readonly IQueue<EventNotificationWorkItem> _notificationQueue;
        private readonly IQueue<WebHookNotification> _webHooksQueue;
        private readonly IQueue<EventUserDescription> _userDescriptionQueue;
        private readonly IMetricsClient _metricsClient;

        public StatusController(SystemHealthChecker healthChecker, IQueue<EventPost> eventQueue, IQueue<MailMessage> mailQueue,
            IQueue<EventNotificationWorkItem> notificationQueue, IQueue<WebHookNotification> webHooksQueue, IQueue<EventUserDescription> userDescriptionQueue, IMetricsClient metricsClient) {
            _healthChecker = healthChecker;
            _eventQueue = eventQueue;
            _mailQueue = mailQueue;
            _notificationQueue = notificationQueue;
            _webHooksQueue = webHooksQueue;
            _userDescriptionQueue = userDescriptionQueue;
            _metricsClient = metricsClient;
        }

        [HttpGet]
        [Route(API_PREFIX + "/status")]
        public IHttpActionResult Index() {
            var result = _healthChecker.CheckAll();
            if (!result.IsHealthy)
                return StatusCodeWithMessage(HttpStatusCode.ServiceUnavailable, result.Message);

            return Ok(new {
                Message = "All Systems Check",
                Version = Settings.Current.Version
            });
        }

        [HttpGet]
        [Route(API_PREFIX + "/queue-stats")]
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
        [Route(API_PREFIX + "/metric-stats")]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult MetricStats() {
            var metricsClient = _metricsClient as InMemoryMetricsClient;
            if (metricsClient == null)
                return Ok();

            return Ok(metricsClient.GetMetricStats());
        }
    }
}
