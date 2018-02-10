using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Exceptionless.Core.Services {
    public class MessageService : IDisposable, IStartupAction {
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IConnectionMapping _connectionMapping;
        private readonly ILogger _logger;

        public MessageService(IStackRepository stackRepository, IEventRepository eventRepository, IConnectionMapping connectionMapping, ILoggerFactory loggerFactory) {
            _stackRepository = stackRepository;
            _eventRepository = eventRepository;
            _connectionMapping = connectionMapping;
            _logger = loggerFactory?.CreateLogger<MessageService>() ?? NullLogger<MessageService>.Instance;
        }

        public Task RunAsync(CancellationToken shutdownToken = default) {
            if (!Settings.Current.EnableRepositoryNotifications)
                return Task.CompletedTask;

            if (_stackRepository is StackRepository sr)
                sr.BeforePublishEntityChanged.AddHandler(BeforePublishStackEntityChanged);
            if (_eventRepository is EventRepository er)
                er.BeforePublishEntityChanged.AddHandler(BeforePublishEventEntityChanged);

            return Task.CompletedTask;
        }

        private async Task BeforePublishStackEntityChanged(object sender, BeforePublishEntityChangedEventArgs<Stack> args) {
            args.Cancel = await GetNumberOfListeners(args.Message).AnyContext() == 0;
            if (args.Cancel && _logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace("Cancelled Stack Entity Changed Message: {@Message}", args.Message);
        }

        private async Task BeforePublishEventEntityChanged(object sender, BeforePublishEntityChangedEventArgs<PersistentEvent> args) {
            args.Cancel = await GetNumberOfListeners(args.Message).AnyContext() == 0;
            if (args.Cancel && _logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace("Cancelled Persistent Event Entity Changed Message: {@Message}", args.Message);
        }

        private Task<int> GetNumberOfListeners(EntityChanged message) {
            var entityChanged = ExtendedEntityChanged.Create(message, false);
            if (String.IsNullOrEmpty(entityChanged.OrganizationId))
                return Task.FromResult(1); // Return 1 as we have no idea if people are listening.

            return _connectionMapping.GetGroupConnectionCountAsync(entityChanged.OrganizationId);
        }

        public void Dispose() {
            if (_stackRepository is StackRepository sr)
                sr.BeforePublishEntityChanged.RemoveHandler(BeforePublishStackEntityChanged);
            if (_eventRepository is EventRepository er)
                er.BeforePublishEntityChanged.RemoveHandler(BeforePublishEventEntityChanged);
        }
    }
}