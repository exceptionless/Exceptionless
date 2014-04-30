using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Extensions;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using NLog.Fluent;

namespace Exceptionless.Core.Queues {
    public class ServiceBusQueue<T> : IQueue<T> where T: class {
        private readonly string _queueName;
        private readonly NamespaceManager _namespaceManager;
        private readonly QueueClient _queueClient;
        private readonly BlockingCollection<Worker> _workers = new BlockingCollection<Worker>();
        private readonly QueueDescription _queueDescription;
        private int _enqueuedCounter = 0;
        private int _dequeuedCounter = 0;
        private int _completedCounter = 0;
        private int _abandonedCounter = 0;
        private int _workerErrorsCounter = 0;
        private bool _isListening = false;

        public ServiceBusQueue(string connectionString, string queueName = null, int retries = 2, int workItemTimeoutMilliseconds = 60 * 1000, bool shouldRecreate = false, RetryPolicy retryPolicy = null) {
            _queueName = queueName ?? typeof(T).Name;
            _namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);

            if (_namespaceManager.QueueExists(_queueName) && shouldRecreate)
                _namespaceManager.DeleteQueue(_queueName);

            if (!_namespaceManager.QueueExists(_queueName)) {
                _queueDescription = new QueueDescription(_queueName) {
                    MaxDeliveryCount = retries + 1,
                    LockDuration = TimeSpan.FromMilliseconds(workItemTimeoutMilliseconds)
                };
                _namespaceManager.CreateQueue(_queueDescription);
            } else {
                _queueDescription = _namespaceManager.GetQueue(_queueName);
                _queueDescription.MaxDeliveryCount = retries + 1;
                _queueDescription.LockDuration = TimeSpan.FromMilliseconds(workItemTimeoutMilliseconds);
            }

            _queueClient = QueueClient.CreateFromConnectionString(connectionString, _queueDescription.Path);
            if (retryPolicy != null)
                _queueClient.RetryPolicy = retryPolicy;
        }

        public long Count {
            get { return _namespaceManager.GetQueue(_queueName).MessageCountDetails.ScheduledMessageCount; }
        }

        public long DeadletterCount {
            get { return _namespaceManager.GetQueue(_queueName).MessageCountDetails.DeadLetterMessageCount; }
        }

        public long ActiveCount {
            get { return _namespaceManager.GetQueue(_queueName).MessageCountDetails.ActiveMessageCount; }
        }

        public int Enqueued { get { return _enqueuedCounter; } }
        public int Dequeued { get { return _dequeuedCounter; } }
        public int Completed { get { return _completedCounter; } }
        public int Abandoned { get { return _abandonedCounter; } }
        public int WorkerErrors { get { return _workerErrorsCounter; } }

        private Task OnMessage(BrokeredMessage message) {
            _isListening = true;
            Interlocked.Increment(ref _dequeuedCounter);
            var data = message.GetBody<T>();

            // get a random worker
            var worker = _workers.ToList().Random();
            if (worker == null) {
                message.Abandon();
                return Task.FromResult(0);
            }

            var workItem = new QueueEntry<T>(message.LockToken.ToString(), data, this);
            try {
                worker.Action(workItem);
                if (worker.AutoComplete)
                    workItem.CompleteAsync().Wait();
            } catch (Exception ex) {
                Interlocked.Increment(ref _workerErrorsCounter);
                Log.Error().Exception(ex).Message("Error sending work item to worker: {0}", ex.Message).Write();
                workItem.AbandonAsync().Wait();
            }

            return Task.FromResult(0);
        }

        public Task EnqueueAsync(T data) {
            Interlocked.Increment(ref _enqueuedCounter);
            return _queueClient.SendAsync(new BrokeredMessage(data));
        }

        public void StartWorking(Action<QueueEntry<T>> handler, bool autoComplete = false) {
            _workers.Add(new Worker { Action = handler, AutoComplete = autoComplete });
            if (_workers.Count > 0 && !_isListening)
                _queueClient.OnMessageAsync(OnMessage);
        }

        public async Task<QueueEntry<T>> DequeueAsync(int millisecondsTimeout = 30 * 1000) {
            using (var msg = await _queueClient.ReceiveAsync(TimeSpan.FromMilliseconds(millisecondsTimeout))) {
                if (msg == null)
                    return null;

                Interlocked.Increment(ref _dequeuedCounter);
                var data = msg.GetBody<T>();
                return new QueueEntry<T>(msg.LockToken.ToString(), data, this);
            }
        }

        public Task CompleteAsync(string id) {
            Interlocked.Increment(ref _completedCounter);
            return _queueClient.CompleteAsync(new Guid(id));
        }

        public Task AbandonAsync(string id) {
            Interlocked.Increment(ref _abandonedCounter);
            return _queueClient.AbandonAsync(new Guid(id));
        }

        private class Worker {
            public bool AutoComplete { get; set; }
            public Action<QueueEntry<T>> Action { get; set; }
        }
    }
}
