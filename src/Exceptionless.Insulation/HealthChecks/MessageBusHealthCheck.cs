using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.DateTimeExtensions;
using Foundatio.Hosting.Startup;
using Foundatio.Messaging;
using Foundatio.Utility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Insulation.HealthChecks {
    public class MessageBusHealthCheck : IStartupAction, IHealthCheck {
        private readonly IMessageBus _messageBus;
        private readonly ILogger _logger;
        private readonly string _id = Guid.NewGuid().ToString();
        private DateTime _lastSentNotification = DateTime.MinValue;
        private DateTime _lastReceivedNotification = DateTime.MinValue;

        public MessageBusHealthCheck(IMessageBus messageBus, ILoggerFactory loggerFactory) {
            _messageBus = messageBus;
            _logger = loggerFactory.CreateLogger<MessageBusHealthCheck>();
            _messageBus.SubscribeAsync<HealthCheckNotification>(OnHealthCheckNotification);
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            var utcNow = SystemClock.UtcNow;
            var previousSentNotification = _lastSentNotification;
            try {
                await _messageBus.PublishAsync(new HealthCheckNotification { Id = _id, Sent = utcNow }).AnyContext();
                _lastSentNotification = utcNow;
            } catch (Exception ex) {
                return HealthCheckResult.Unhealthy("Unable to publish Health Check notification.", ex);
            }
            
            if (_lastReceivedNotification == DateTime.MinValue)
                return HealthCheckResult.Degraded("Subscriber hasn't received initial Health Check.");

            if (_lastReceivedNotification.IsBefore(previousSentNotification))
                return HealthCheckResult.Degraded($"Subscriber hasn't received Health Check since: {previousSentNotification}");
            
            return HealthCheckResult.Healthy();
        }
        
        private Task OnHealthCheckNotification(HealthCheckNotification notification, CancellationToken cancellationToken) {
            if (!String.Equals(notification.Id, _id))
                return Task.CompletedTask;
            
            _logger.LogTrace("Received Health Check Notification: {SentOn}", notification.Sent);
            _lastReceivedNotification = SystemClock.UtcNow;
            return Task.CompletedTask;
        }

        public Task RunAsync(CancellationToken shutdownToken = default) {
            return CheckHealthAsync(new HealthCheckContext(), shutdownToken);
        }
    }

    public class HealthCheckNotification {
        public string Id { get; set; }
        public DateTime Sent { get; set; }
    }
}