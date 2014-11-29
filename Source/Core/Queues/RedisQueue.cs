using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Component;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Lock;
using Nito.AsyncEx;
using NLog.Fluent;
using StackExchange.Redis;

namespace Exceptionless.Core.Queues {
    public class RedisQueue<T> : IDisposable, IQueue<T> where T: class {
        private readonly string _queueName;
        private readonly IDatabase _db;
        private readonly ISubscriber _subscriber;
        private readonly RedisCacheClient _cache;
        private readonly ILockProvider _lockProvider;
        private Func<QueueEntry<T>, Task> _workerAction;
        private bool _workerAutoComplete = false;
        private long _enqueuedCount;
        private long _dequeuedCount;
        private long _completedCount;
        private long _abandonedCount;
        private long _workerErrorCount;
        private readonly TimeSpan _payloadTtl = TimeSpan.FromDays(3);
        private readonly TimeSpan _workItemTimeout = TimeSpan.FromMinutes(10);
        private readonly int _retries = 2;
        private readonly TimeSpan _deadLetterTtl = TimeSpan.FromDays(1);
        private CancellationTokenSource _workerCancellationTokenSource;
        private CancellationTokenSource _queueDisposedCancellationTokenSource;
        private readonly AsyncAutoResetEvent _autoEvent = new AsyncAutoResetEvent(false);

        public RedisQueue(ConnectionMultiplexer connection, string queueName = null, int retries = 2, TimeSpan? workItemTimeout = null, TimeSpan? deadLetterTimeToLive = null, bool runMaintenanceTasks = true) {
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
            if (workItemTimeout.HasValue)
                _workItemTimeout = workItemTimeout.Value;
            if (deadLetterTimeToLive.HasValue)
                _deadLetterTtl = deadLetterTimeToLive.Value;
            if (runMaintenanceTasks) {
                _queueDisposedCancellationTokenSource = new CancellationTokenSource();
                TaskHelper.RunPeriodic(DoMaintenanceWork, _workItemTimeout > TimeSpan.FromSeconds(1) ? _workItemTimeout : TimeSpan.FromSeconds(1), _queueDisposedCancellationTokenSource.Token, TimeSpan.FromMilliseconds(100));
            }
        }

        public Task<long> GetQueueCountAsync() {
            return _db.ListLengthAsync(QueueListName);
        }

        public Task<long> GetWorkingCountAsync() {
            return _db.ListLengthAsync(WorkListName);
        }

        public Task<long> GetDeadletterCountAsync() {
            return _db.ListLengthAsync(DeadListName);
        }

        public long EnqueuedCount { get { return _enqueuedCount; } }
        public long DequeuedCount { get { return _dequeuedCount; } }
        public long CompletedCount { get { return _completedCount; } }
        public long AbandonedCount { get { return _abandonedCount; } }
        public long WorkerErrorCount { get { return _workerErrorCount; } }

        private string QueueListName { get; set; }
        private string WorkListName { get; set; }
        private string WaitListName { get; set; }
        private string DeadListName { get; set; }

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
            Debug.WriteLine("EnqueueAsync: " + id);
            bool success = await _db.StringSetAsync(GetPayloadKey(id), data.ToJson(), _payloadTtl, When.NotExists);
            if (!success)
                throw new InvalidOperationException("Attempt to set payload failed.");
            await _db.ListLeftPushAsync(QueueListName, id);
            await _subscriber.PublishAsync(GetTopicName(), id);
            Interlocked.Increment(ref _enqueuedCount);
            Debug.WriteLine("EnqueueAsync Done: " + id);
        }

        public void StartWorking(Action<QueueEntry<T>> handler, bool autoComplete = false) {
            StartWorking(async entry => handler(entry), autoComplete);
        }

        public void StartWorking(Func<QueueEntry<T>, Task> handler, bool autoComplete = false) {
            if (handler == null)
                throw new ArgumentNullException("handler");

            _workerAction = handler;
            _workerAutoComplete = autoComplete;

            if (_workerCancellationTokenSource != null)
                return;

            _subscriber.Subscribe(GetTopicName(), OnTopicMessage);
            _workerCancellationTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(() => WorkerLoop(_workerCancellationTokenSource.Token));
        }

        public void StopWorking() {
            _workerAction = null;
            _subscriber.UnsubscribeAll();

            if (_workerCancellationTokenSource != null)
                _workerCancellationTokenSource.Cancel();
        }

        public async Task<QueueEntry<T>> DequeueAsync(TimeSpan? timeout = null) {
            if (!timeout.HasValue)
                timeout = TimeSpan.FromSeconds(30);
            Debug.WriteLine("DequeueAsync");
            RedisValue value = await _db.ListRightPopLeftPushAsync(QueueListName, WorkListName);

            DateTime started = DateTime.Now;
            while (timeout > TimeSpan.Zero && value.IsNullOrEmpty && DateTime.Now.Subtract(started) < timeout) {
                Debug.WriteLine("Waiting for queue item...");
                Task.WaitAny(_autoEvent.WaitAsync(timeout.Value), Task.Delay(1000));
                value = await _db.ListRightPopLeftPushAsync(QueueListName, WorkListName);
            }

            if (value.IsNullOrEmpty)
                return null;

            Debug.WriteLine("Dequeued item");
            IDisposable workItemLock;
            try {
                Debug.WriteLine("Getting item lock...");
                workItemLock = _lockProvider.AcquireLock(value, _workItemTimeout, TimeSpan.FromSeconds(2));
                if (workItemLock == null)
                    return null;
            } catch (TimeoutException) {
                return null;
            }

            Debug.WriteLine("Got item lock");
            await _db.StringSetAsync(GetDequeuedTimeKey(value), DateTime.Now.ToJson());
            RedisValue payloadValue = await _db.StringGetAsync(GetPayloadKey(value));
            if (payloadValue.IsNullOrEmpty) {
                await _db.ListRemoveAsync(WorkListName, value);
                return null;
            }

            try {
                var payload = ((string)payloadValue).FromJson<T>();

                Interlocked.Increment(ref _dequeuedCount);
                return new RedisQueueEntry<T>(value, payload, this, workItemLock);
            } catch (Exception ex) {
                Debug.WriteLine(ex.ToString());
                throw;
            }
        }

        public async Task CompleteAsync(string id) {
            Debug.WriteLine("Complete: " + id);
            await _db.ListRemoveAsync(WorkListName, id);
            await _db.KeyDeleteAsync(GetPayloadKey(id));
            await _db.KeyDeleteAsync(GetAttemptsKey(id));
            await _db.KeyDeleteAsync(GetDequeuedTimeKey(id));
            Interlocked.Increment(ref _completedCount);
            Debug.WriteLine("Complete Done: " + id);
        }

        public async Task AbandonAsync(string id) {
            Debug.WriteLine("AbandonAsync: " + id);
            var attemptsValue = await _db.StringGetAsync(GetAttemptsKey(id));
            int attempts = 1;
            if (attemptsValue.HasValue)
                attempts = (int)attemptsValue + 1;
            Debug.WriteLine("Attempts: " + attempts);

            if (attempts > _retries) {
                Debug.WriteLine("Exceeded retry limit moving to deadletter: " + id);
                await _db.ListRemoveAsync(WorkListName, id);
                await _db.ListLeftPushAsync(DeadListName, id);
                await _db.KeyExpireAsync(GetPayloadKey(id), _deadLetterTtl);
            } else {
                Debug.WriteLine("Adding item back to queue for retry: " + id);
                var tx = _db.CreateTransaction();
                tx.ListRemoveAsync(WorkListName, id);
                tx.ListLeftPushAsync(QueueListName, id);
                var success = await tx.ExecuteAsync();
                if (!success)
                    throw new Exception("Unable to abandon.");

                await _db.StringIncrementAsync(GetAttemptsKey(id));
                await _subscriber.PublishAsync(GetTopicName(), id);
            }

            Interlocked.Increment(ref _abandonedCount);
            Debug.WriteLine("AbandonAsync Complete: " + id);
        }

        public Task<IEnumerable<T>> GetDeadletterItemsAsync() {
            throw new NotImplementedException();
        }

        public async Task ResetQueueAsync() {
            await _db.KeyDeleteAsync(QueueListName);
            await _db.KeyDeleteAsync(WorkListName);
            await _db.KeyDeleteAsync(WaitListName);
            await _db.KeyDeleteAsync(DeadListName);
            _enqueuedCount = 0;
            _dequeuedCount = 0;
            _completedCount = 0;
            _abandonedCount = 0;
            _workerErrorCount = 0;
        }

        private void OnTopicMessage(RedisChannel redisChannel, RedisValue redisValue) {
            Debug.WriteLine("OnMessage: " + Thread.CurrentThread.ManagedThreadId);
            _autoEvent.Set();
        }

        private async Task WorkerLoop(CancellationToken token) {
            Debug.WriteLine("WorkerLoop Start: " + Thread.CurrentThread.ManagedThreadId);
            while (!token.IsCancellationRequested && _workerAction != null) {
                await _autoEvent.WaitAsync(token);

                Debug.WriteLine("WorkerLoop Signaled");
                QueueEntry<T> queueEntry = null;
                try {
                    queueEntry = await DequeueAsync(TimeSpan.Zero);
                } catch (TimeoutException) { }

                if (queueEntry == null)
                    continue;

                try {
                    await _workerAction(queueEntry);
                    if (_workerAutoComplete)
                        await queueEntry.CompleteAsync();
                } catch (Exception ex) {
                    Debug.WriteLine("Worker error: {0}", ex.Message);
                    Log.Error().Exception(ex).Message("Worker error: {0}", ex.Message).Write();
                    queueEntry.AbandonAsync().Wait();
                    Interlocked.Increment(ref _workerErrorCount);
                }
            }
        }

        private async Task DoMaintenanceWork() {
            Debug.WriteLine("OnMaintenance");
            var workItems = await _db.ListRangeAsync(WorkListName);
            foreach (var workItem in workItems) {
                var dequeuedTime = _cache.Get<DateTime?>(GetDequeuedTimeKey(workItem));
                Debug.WriteLine("Dequeue time: " + dequeuedTime);
                if (!dequeuedTime.HasValue || DateTime.Now.Subtract(dequeuedTime.Value) <= _workItemTimeout)
                    continue;

                Debug.WriteLine("Getting work time out lock");
                try {
                    using (_lockProvider.AcquireLock(workItem, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5))) {
                        Debug.WriteLine("Got item lock for work time out");
                        await AbandonAsync(workItem);
                        Debug.WriteLine("Abandoned item item lock for work time out");
                    }
                } catch { }
            }
        }

        public void Dispose() {
            Debug.WriteLine("Dispose");
            StopWorking();
            if (_queueDisposedCancellationTokenSource != null)
                _queueDisposedCancellationTokenSource.Cancel();
        }

        private class RedisQueueEntry<T> : QueueEntry<T> where T : class {
            private readonly IDisposable _workItemLock;

            public RedisQueueEntry(string id, T value, IQueue<T> queue, IDisposable workItemLock) : base(id, value, queue) {
                _workItemLock = workItemLock;
            }

            public override async Task CompleteAsync() {
                Debug.WriteLine("CompleteEntry");
                await base.CompleteAsync();
                ReleaseLock();
            }

            public async override Task AbandonAsync() {
                Debug.WriteLine("AbandonEntry");
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
