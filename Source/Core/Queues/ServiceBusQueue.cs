using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using NLog.Fluent;

namespace Exceptionless.Core.Queues {
    public class ServiceBusQueue<T> : IQueue<T> where T: class {
        private readonly string _queueName;
        private readonly NamespaceManager _namespaceManager;
        private readonly QueueClient _queueClient;
        private Func<QueueEntry<T>, Task> _workerAction;
        private bool _workerAutoComplete = false;
        private bool _isWorking;
        private static object _workerLock = new object();
        private QueueDescription _queueDescription;
        private long _enqueuedCount = 0;
        private long _dequeuedCount = 0;
        private long _completedCount = 0;
        private long _abandonedCount = 0;
        private long _workerErrorCount = 0;
        private bool _isListening = false;
        private readonly int _retries;
        private readonly int _workItemTimeoutMilliseconds;

        public ServiceBusQueue(string connectionString, string queueName = null, int retries = 2, int workItemTimeoutMilliseconds = 60 * 1000, bool shouldRecreate = false, RetryPolicy retryPolicy = null) {
            _queueName = queueName ?? typeof(T).Name;
            _namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
            _retries = retries;
            _workItemTimeoutMilliseconds = workItemTimeoutMilliseconds;

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

        public async Task ResetQueueAsync() {
            if (await _namespaceManager.QueueExistsAsync(_queueName))
                await _namespaceManager.DeleteQueueAsync(_queueName);

            _queueDescription = new QueueDescription(_queueName) {
                MaxDeliveryCount = _retries + 1,
                LockDuration = TimeSpan.FromMilliseconds(_workItemTimeoutMilliseconds)
            };
            await _namespaceManager.CreateQueueAsync(_queueDescription);

            _enqueuedCount = 0;
            _dequeuedCount = 0;
            _completedCount = 0;
            _abandonedCount = 0;
            _workerErrorCount = 0;
        }

        public long EnqueuedCount { get { return _enqueuedCount; } }
        public long DequeuedCount { get { return _dequeuedCount; } }
        public long CompletedCount { get { return _completedCount; } }
        public long AbandonedCount { get { return _abandonedCount; } }
        public long WorkerErrorCount { get { return _workerErrorCount; } }

        public async Task<long> GetQueueCountAsync() { return (await _namespaceManager.GetQueueAsync(_queueName)).MessageCountDetails.ScheduledMessageCount; }
        public async Task<long> GetWorkingCountAsync() { return (await _namespaceManager.GetQueueAsync(_queueName)).MessageCountDetails.ActiveMessageCount; }
        public async Task<long> GetDeadletterCountAsync() { return (await _namespaceManager.GetQueueAsync(_queueName)).MessageCountDetails.DeadLetterMessageCount; }

        public Task<IEnumerable<T>> GetDeadletterItemsAsync() {
            throw new NotImplementedException();
        }

        private async Task OnMessage(BrokeredMessage message) {
            if (_workerAction == null)
                return;

            Interlocked.Increment(ref _dequeuedCount);
            var data = message.GetBody<T>();

            var workItem = new QueueEntry<T>(message.LockToken.ToString(), data, this);
            try {
                await _workerAction(workItem);
                if (_workerAutoComplete)
                    await workItem.CompleteAsync();
            } catch (Exception ex) {
                Interlocked.Increment(ref _workerErrorCount);
                Log.Error().Exception(ex).Message("Error sending work item to worker: {0}", ex.Message).Write();
                workItem.AbandonAsync().Wait();
            }
        }

        public Task EnqueueAsync(T data) {
            Interlocked.Increment(ref _enqueuedCount);
            return _queueClient.SendAsync(new BrokeredMessage(data));
        }

        public void StartWorking(Action<QueueEntry<T>> handler, bool autoComplete = false) {
            StartWorking(async entry => handler(entry), autoComplete);
        }

        public void StartWorking(Func<QueueEntry<T>, Task> handler, bool autoComplete = false) {
            if (_isWorking)
                throw new ApplicationException("Already working.");

            lock (_workerLock) {
                Debug.WriteLine("StartWorking: " + Thread.CurrentThread.ManagedThreadId);
                _isWorking = true;
                _workerAction = handler;
                _workerAutoComplete = autoComplete;
                _queueClient.OnMessageAsync(OnMessage);
            }
        }

        public void StopWorking() {
            if (!_isWorking)
                return;

            lock (_workerLock) {
                _isWorking = false;
                _workerAction = null;
            }
        }

        public async Task<QueueEntry<T>> DequeueAsync(TimeSpan? timeout = null) {
            if (!timeout.HasValue)
                timeout = TimeSpan.FromSeconds(30);

            using (var msg = await _queueClient.ReceiveAsync(timeout.Value)) {
                if (msg == null)
                    return null;

                Interlocked.Increment(ref _dequeuedCount);
                var data = msg.GetBody<T>();
                return new QueueEntry<T>(msg.LockToken.ToString(), data, this);
            }
        }

        public Task CompleteAsync(string id) {
            Interlocked.Increment(ref _completedCount);
            return _queueClient.CompleteAsync(new Guid(id));
        }

        public Task AbandonAsync(string id) {
            Interlocked.Increment(ref _abandonedCount);
            return _queueClient.AbandonAsync(new Guid(id));
        }

        public void Dispose() {
            StopWorking();
            _queueClient.Close();
        }
    }
}
