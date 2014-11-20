using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Lock;
using NLog.Fluent;
using StackExchange.Redis;

namespace Exceptionless.Core.Queues {
    public class RedisQueue<T> : IQueue<T> where T: class {
        private readonly string _queueName;
        private readonly IDatabase _db;
        private readonly ISubscriber _subscriber;
        private readonly RedisCacheClient _cache;
        private readonly ILockProvider _lockProvider;
        private readonly BlockingCollection<Worker> _workers = new BlockingCollection<Worker>();
        private int _enqueuedCounter;
        private int _dequeuedCounter;
        private int _completedCounter;
        private int _abandonedCounter;
        private int _workerErrorsCounter;
        private bool _isListening;
        private readonly TimeSpan _payloadTtl = TimeSpan.FromDays(3);
        private readonly TimeSpan _workItemTimeout;
        private readonly int _retries = 2;

        public RedisQueue(ConnectionMultiplexer connection, string queueName = null, int retries = 2, int workItemTimeoutMilliseconds = 60 * 1000) {
            _db = connection.GetDatabase();
            _subscriber = connection.GetSubscriber();
            _cache = new RedisCacheClient(_db);
            _lockProvider = new CacheLockProvider(_cache);
            _queueName = queueName ?? typeof(T).Name;
            _queueName = _queueName.RemoveWhiteSpace().Replace(':', '-');
            QueueListName = "q:" + _queueName + ":in";
            WorkListName = "q:" + _queueName + ":work";
            WaitListName = "q:" + _queueName + ":wait";
            DeadListName = "q:" + _queueName + ":dead";
            _retries = retries;
            _workItemTimeout = TimeSpan.FromMilliseconds(workItemTimeoutMilliseconds);
        }

        public long Count {
            get { return _db.ListLength(QueueListName); }
        }

        public long DeadletterCount {
            get { return _db.ListLength(DeadListName); }
        }

        public long WorkingCount {
            get { return _db.ListLength(WorkListName); }
        }

        public int Enqueued { get { return _enqueuedCounter; } }
        public int Dequeued { get { return _dequeuedCounter; } }
        public int Completed { get { return _completedCounter; } }
        public int Abandoned { get { return _abandonedCounter; } }
        public int WorkerErrors { get { return _workerErrorsCounter; } }

        private string QueueListName { get; set; }
        private string WorkListName { get; set; }
        private string WaitListName { get; set; }
        private string DeadListName { get; set; }

        public void DeleteQueue() {
            _db.KeyDelete(QueueListName);
            _db.KeyDelete(WorkListName);
            _db.KeyDelete(WaitListName);
            _db.KeyDelete(DeadListName);
        }

        private string GetPayloadKey(string id) {
            return String.Concat("q:", _queueName, ":", id);
        }

        private string GetAttemptsKey(string id) {
            return String.Concat("q:", _queueName, ":", id, ":attempts");
        }

        private string GetDequeuedTimeKey(string id) {
            return String.Concat("q:", _queueName, ":", id, ":dequeued");
        }

        private string GetWaitTimeKey(string id) {
            return String.Concat("q:", _queueName, ":", id, ":wait");
        }

        private string GetTopicName() {
            return String.Concat("q:", _queueName, ":in");
        }

        public async Task EnqueueAsync(T data) {
            string id = Guid.NewGuid().ToString("N");
            bool success = await _db.StringSetAsync(GetPayloadKey(id), data.ToJson(), _payloadTtl, When.NotExists);
            if (!success)
                throw new InvalidOperationException("Attempt to set payload failed.");
            await _db.ListLeftPushAsync(QueueListName, id);
            await _subscriber.PublishAsync(GetTopicName(), id);
            Interlocked.Increment(ref _enqueuedCounter);
        }

        public void StartWorking(Action<QueueEntry<T>> handler, bool autoComplete = false) {
            _workers.Add(new Worker { Action = handler, AutoComplete = autoComplete });

            if (_workers.Count > 0 && !_isListening) {
                _isListening = true;
                _subscriber.Subscribe(GetTopicName(), OnTopicMessage);
            }
        }

        public async Task<QueueEntry<T>> DequeueAsync(int millisecondsTimeout = 30 * 1000) {
            RedisValue value = await _db.ListRightPopLeftPushAsync(QueueListName, WorkListName);

            DateTime started = DateTime.Now;
            while (millisecondsTimeout > 0 && value.IsNullOrEmpty && DateTime.Now.Subtract(started).TotalMilliseconds < millisecondsTimeout) {
                Thread.Sleep(1000);
                value = await _db.ListRightPopLeftPushAsync(QueueListName, WorkListName);
            }

            if (value.IsNullOrEmpty)
                return null;

            IDisposable workItemLock;
            try {
                workItemLock = _lockProvider.AcquireLock(value, _workItemTimeout.Add(TimeSpan.FromMinutes(1)), TimeSpan.FromSeconds(2));
                if (workItemLock == null)
                    return null;
            } catch (TimeoutException) {
                return null;
            }

            RedisValue payloadValue = await _db.StringGetAsync(GetPayloadKey(value));
            if (payloadValue.IsNullOrEmpty) {
                await _db.ListRemoveAsync(WorkListName, value);
                return null;
            }

            var payload = ((string)payloadValue).FromJson<T>();

            Interlocked.Increment(ref _dequeuedCounter);
            return new RedisQueueEntry<T>(value, payload, this, workItemLock);
        }

        public async Task CompleteAsync(string id) {
            await _db.ListRemoveAsync(WorkListName, id);
            await _db.KeyDeleteAsync(GetPayloadKey(id));
            await _db.KeyDeleteAsync(GetAttemptsKey(id));
            await _db.KeyDeleteAsync(GetDequeuedTimeKey(id));
            Interlocked.Increment(ref _completedCounter);
        }

        public Task AbandonAsync(string id) {
            Interlocked.Increment(ref _abandonedCounter);
            return Task.FromResult(0);
            //return _queueClient.AbandonAsync(new Guid(id));
        }

        private async void OnTopicMessage(RedisChannel redisChannel, RedisValue redisValue) {
            var workItem = await DequeueAsync(0);
            if (workItem == null)
                return;

            // get a random worker
            var worker = _workers.ToList().Random();
            if (worker == null) {
                await workItem.AbandonAsync();
                return;
            }

            try {
                worker.Action(workItem);
                if (worker.AutoComplete)
                    await workItem.CompleteAsync();
            } catch (Exception ex) {
                Interlocked.Increment(ref _workerErrorsCounter);
                Log.Error().Exception(ex).Message("Error sending work item to worker: {0}", ex.Message).Write();
                workItem.AbandonAsync().Wait();
            }
        }

        private class Worker {
            public bool AutoComplete { get; set; }
            public Action<QueueEntry<T>> Action { get; set; }
        }

        private class RedisQueueEntry<T> : QueueEntry<T> where T : class {
            private readonly IDisposable _workItemLock;

            public RedisQueueEntry(string id, T value, IQueue<T> queue, IDisposable workItemLock) : base(id, value, queue) {
                _workItemLock = workItemLock;
            }

            public override async Task CompleteAsync() {
                await base.CompleteAsync();
                ReleaseLock();
            }

            public async override Task AbandonAsync() {
                await base.AbandonAsync();
                ReleaseLock();
            }

            private void ReleaseLock() {
                if (_workItemLock != null)
                    _workItemLock.Dispose();
            }

            public override void Dispose() {
                ReleaseLock();
                base.Dispose();
            }
        }
    }
}
