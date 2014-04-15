using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace Exceptionless.Core.Queues {
    public class ServiceBusQueue<T> : IQueue<T> {
        private readonly string _queueName;
        private readonly NamespaceManager _namespaceManager;
        private readonly QueueClient _queueClient;

        public ServiceBusQueue(string connectionString, string queueName = null) {
            _queueName = queueName ?? typeof(T).Name;
            _namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
            if (!_namespaceManager.QueueExists(_queueName))
                _namespaceManager.CreateQueue(_queueName);

            _queueClient = QueueClient.CreateFromConnectionString(connectionString, _queueName);
        }

        public Task EnqueueAsync(T data) {
            return _queueClient.SendAsync(new BrokeredMessage(data));
        }

        public async Task<WorkItem<T>> DequeueAsync() {
            using (var msg = await _queueClient.ReceiveAsync()) {
                if (msg == null)
                    return null;

                var data = msg.GetBody<T>();
                return new WorkItem<T>(msg.LockToken.ToString(), data, this);
            }
        }

        public Task CompleteAsync(string id) {
            return _queueClient.CompleteAsync(new Guid(id));
        }

        public Task AbandonAsync(string id) {
            return _queueClient.AbandonAsync(new Guid(id));
        }
    }
}
