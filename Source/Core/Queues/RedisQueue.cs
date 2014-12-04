using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Component;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Lock;
using Nito.AsyncEx;
using NLog.Fluent;
using StackExchange.Redis;

namespace Exceptionless.Core.Queues {
    public class RedisQueue<T> : IQueue<T> where T: class {
        private readonly string _queueName;
        private readonly IDatabase _db;
        private readonly ISubscriber _subscriber;
        private readonly RedisCacheClient _cache;
        private readonly ILockProvider _lockProvider;
        private Action<QueueEntry<T>> _workerAction;
        private bool _workerAutoComplete;
        private long _enqueuedCount;
        private long _dequeuedCount;
        private long _completedCount;
        private long _abandonedCount;
        private long _workerErrorCount;
        private readonly TimeSpan _payloadTtl;
        private readonly TimeSpan _workItemTimeout = TimeSpan.FromMinutes(10);
        private readonly TimeSpan _retryDelay = TimeSpan.FromMinutes(1);
        private readonly int[] _retryMultipliers = { 1, 3, 5, 10 };
        private readonly int _retries = 2;
        private readonly TimeSpan _deadLetterTtl = TimeSpan.FromDays(1);
        private CancellationTokenSource _workerCancellationTokenSource;
        private readonly CancellationTokenSource _queueDisposedCancellationTokenSource;
        private readonly AsyncAutoResetEvent _autoEvent = new AsyncAutoResetEvent(false);

        public RedisQueue(ConnectionMultiplexer connection, string queueName = null, int retries = 2, TimeSpan? retryDelay = null, int[] retryMultipliers = null, TimeSpan? workItemTimeout = null, TimeSpan? deadLetterTimeToLive = null, bool runMaintenanceTasks = true) {
            QueueId = Guid.NewGuid().ToString("N");
            _db = connection.GetDatabase();
            _cache = new RedisCacheClient(_db);
            _lockProvider = new CacheLockProvider(_cache);
            _queueName = queueName ?? typeof(T).Name;
            _queueName = _queueName.RemoveWhiteSpace().Replace(':', '-');
            QueueListName = "q:" + _queueName + ":in";
            WorkListName = "q:" + _queueName + ":work";
            WaitListName = "q:" + _queueName + ":wait";
            DeadListName = "q:" + _queueName + ":dead";
            // TODO: Make queue settings immutable and stored in redis so that multiple clients can't have different settings.
            _retries = retries;
            if (retryDelay.HasValue)
                _retryDelay = retryDelay.Value;
            if (retryMultipliers != null)
                _retryMultipliers = retryMultipliers;
            if (workItemTimeout.HasValue)
                _workItemTimeout = workItemTimeout.Value;
            if (deadLetterTimeToLive.HasValue)
                _deadLetterTtl = deadLetterTimeToLive.Value;

            _payloadTtl = GetPayloadTtl();

            _subscriber = connection.GetSubscriber();
            _subscriber.Subscribe(GetTopicName(), OnTopicMessage);

            if (runMaintenanceTasks) {
                _queueDisposedCancellationTokenSource = new CancellationTokenSource();
                TaskHelper.RunPeriodic(DoMaintenanceWork, _workItemTimeout > TimeSpan.FromSeconds(1) ? _workItemTimeout : TimeSpan.FromSeconds(1), _queueDisposedCancellationTokenSource.Token, TimeSpan.FromMilliseconds(100));
            }

            Log.Trace().Message("Queue {0} created. Retries: {1} Retry Delay: {2}", QueueId, _retries, _retryDelay.ToString()).Write();
        }

        public long GetQueueCount() {
            return _db.ListLength(QueueListName);
        }

        public long GetWorkingCount() {
            return _db.ListLength(WorkListName);
        }

        public long GetDeadletterCount() {
            return _db.ListLength(DeadListName);
        }

        public long EnqueuedCount { get { return _enqueuedCount; } }
        public long DequeuedCount { get { return _dequeuedCount; } }
        public long CompletedCount { get { return _completedCount; } }
        public long AbandonedCount { get { return _abandonedCount; } }
        public long WorkerErrorCount { get { return _workerErrorCount; } }
        public string QueueId { get; private set; }

        private string QueueListName { get; set; }
        private string WorkListName { get; set; }
        private string WaitListName { get; set; }
        private string DeadListName { get; set; }

        private string GetPayloadKey(string id) {
            return String.Concat("q:", _queueName, ":", id);
        }

        private TimeSpan GetPayloadTtl() {
            var ttl = TimeSpan.Zero;
            for (int attempt = 1; attempt <= _retries + 1; attempt++)
                ttl = ttl.Add(GetRetryDelay(attempt));

            // minimum of 7 days for payload
            return TimeSpan.FromMilliseconds(Math.Max(ttl.TotalMilliseconds * 1.5, TimeSpan.FromDays(7).TotalMilliseconds));
        }

        private string GetAttemptsKey(string id) {
            return String.Concat("q:", _queueName, ":", id, ":attempts");
        }

        private TimeSpan GetAttemptsTtl() {
            return _payloadTtl;
        }

        private string GetDequeuedTimeKey(string id) {
            return String.Concat("q:", _queueName, ":", id, ":dequeued");
        }

        private TimeSpan GetDequeuedTimeTtl() {
            return TimeSpan.FromMilliseconds(Math.Max(_workItemTimeout.TotalMilliseconds * 1.5, TimeSpan.FromHours(1).TotalMilliseconds));
        }

        private string GetWaitTimeKey(string id) {
            return String.Concat("q:", _queueName, ":", id, ":wait");
        }

        private TimeSpan GetWaitTimeTtl() {
            return _payloadTtl;
        }

        private string GetTopicName() {
            return String.Concat("q:", _queueName, ":in");
        }

        public string Enqueue(T data) {
            string id = Guid.NewGuid().ToString("N");
            Log.Trace().Message("Queue {0} enqueue item: {1}", _queueName, id).Write();
            bool success = _cache.Add(GetPayloadKey(id), data, _payloadTtl);
            if (!success)
                throw new InvalidOperationException("Attempt to set payload failed.");
            _db.ListLeftPush(QueueListName, id);
            _subscriber.Publish(GetTopicName(), id);
            Interlocked.Increment(ref _enqueuedCount);
            Log.Trace().Message("Enqueue done").Write();

            return id;
        }

        public void StartWorking(Action<QueueEntry<T>> handler, bool autoComplete = false) {
            if (handler == null)
                throw new ArgumentNullException("handler");

            Log.Trace().Message("Queue {0} start working", _queueName).Write();
            _workerAction = handler;
            _workerAutoComplete = autoComplete;

            if (_workerCancellationTokenSource != null)
                return;

            _workerCancellationTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(() => WorkerLoop(_workerCancellationTokenSource.Token));
        }

        public void StopWorking() {
            Log.Trace().Message("Queue {0} stop working", _queueName).Write();
            _workerAction = null;
            _subscriber.UnsubscribeAll();

            if (_workerCancellationTokenSource != null)
                _workerCancellationTokenSource.Cancel();

            _autoEvent.Set();
        }

        public QueueEntry<T> Dequeue(TimeSpan? timeout = null) {
            Log.Trace().Message("Queue {0} dequeuing item (timeout: {1})...", _queueName, timeout != null ? timeout.ToString() : "(none)").Write();
            if (!timeout.HasValue)
                timeout = TimeSpan.FromSeconds(30);
            RedisValue value = _db.ListRightPopLeftPush(QueueListName, WorkListName);
            Log.Trace().Message("Initial list value: {0}", (value.IsNullOrEmpty ? "<null>" : value.ToString())).Write();

            DateTime started = DateTime.UtcNow;
            while (timeout > TimeSpan.Zero && value.IsNullOrEmpty && DateTime.UtcNow.Subtract(started) < timeout) {
                Log.Trace().Message("Waiting to dequeue item...").Write();

                // wait for timeout or signal or dispose
                Task.WaitAny(Task.Delay(timeout.Value), _autoEvent.WaitAsync(_queueDisposedCancellationTokenSource.Token));
                if (_queueDisposedCancellationTokenSource.IsCancellationRequested)
                    return null;

                value = _db.ListRightPopLeftPush(QueueListName, WorkListName);
                Log.Trace().Message("List value: {0}", (value.IsNullOrEmpty ? "<null>" : value.ToString())).Write();
            }

            if (value.IsNullOrEmpty)
                return null;

            Log.Trace().Message("Dequeued item: {0}", value).Write();
            try {
                Log.Trace().Message("Getting item lock...", value).Write();
                IDisposable workItemLock = _lockProvider.AcquireLock(value, _workItemTimeout, TimeSpan.FromSeconds(2));
                if (workItemLock == null)
                    return null;
            } catch (TimeoutException) {
                return null;
            }

            Log.Trace().Message("Got item lock: {0}", value).Write();
            _cache.Set(GetDequeuedTimeKey(value), DateTime.UtcNow, GetDequeuedTimeTtl());

            try {
                var payload = _cache.Get<T>(GetPayloadKey(value));
                if (payload == null) {
                    _db.ListRemove(WorkListName, value);
                    return null;
                }

                Interlocked.Increment(ref _dequeuedCount);
                return new QueueEntry<T>(value, payload, this);
            } catch (Exception ex) {
                Log.Error().Message("Error getting queue payload: {0}", value).Exception(ex).Write();
                throw;
            }
        }

        public void Complete(string id) {
            Log.Trace().Message("Queue {0} complete item: {1}", _queueName, id).Write();
            _db.ListRemove(WorkListName, id);
            _db.KeyDelete(GetPayloadKey(id));
            _db.KeyDelete(GetAttemptsKey(id));
            _db.KeyDelete(GetDequeuedTimeKey(id));
            _db.KeyDelete(GetWaitTimeKey(id));
            _lockProvider.ReleaseLock(id);
            Interlocked.Increment(ref _completedCount);
            Log.Trace().Message("Complete done: {0}", id).Write();
        }

        public void Abandon(string id) {
            Log.Trace().Message("Queue {0} abandon item: {1}", _queueName + ":" + QueueId, id).Write();
            var attemptsValue = _db.StringGet(GetAttemptsKey(id));
            int attempts = 1;
            if (attemptsValue.HasValue)
                attempts = (int)attemptsValue + 1;
            Log.Trace().Message("Item attempts: {0}", attempts).Write();

            var retryDelay = GetRetryDelay(attempts);
            Log.Trace().Message("Retry attempts: {0} delay: {1} allowed: {2}", attempts, retryDelay.ToString(), _retries).Write();
            if (attempts > _retries) {
                Log.Trace().Message("Exceeded retry limit moving to deadletter: {0}", id).Write();
                _db.ListRemove(WorkListName, id);
                _db.ListLeftPush(DeadListName, id);
                _db.KeyExpire(GetPayloadKey(id), _deadLetterTtl);
            } else if (retryDelay > TimeSpan.Zero) {
                Log.Trace().Message("Adding item to wait list for future retry: {0}", id).Write();
                var tx = _db.CreateTransaction();
                tx.ListRemoveAsync(WorkListName, id);
                tx.ListLeftPushAsync(WaitListName, id);
                var success = tx.Execute();
                if (!success)
                    throw new Exception("Unable to move item to wait list.");

                _cache.Set(GetWaitTimeKey(id), DateTime.UtcNow.Add(retryDelay), GetWaitTimeTtl());
                _db.StringIncrement(GetAttemptsKey(id));
                _db.KeyExpire(GetAttemptsKey(id), GetAttemptsTtl());
            } else {
                Log.Trace().Message("Adding item back to queue for retry: {0}", id).Write();
                var tx = _db.CreateTransaction();
                tx.ListRemoveAsync(WorkListName, id);
                tx.ListLeftPushAsync(QueueListName, id);
                var success = tx.Execute();
                if (!success)
                    throw new Exception("Unable to move item to queue list.");

                _db.StringIncrement(GetAttemptsKey(id));
                _db.KeyExpire(GetAttemptsKey(id), GetAttemptsTtl());
                _subscriber.Publish(GetTopicName(), id);
            }

            _lockProvider.ReleaseLock(id);
            Interlocked.Increment(ref _abandonedCount);
            Log.Trace().Message("Abondon complete: {0}", id).Write();
        }

        private TimeSpan GetRetryDelay(int attempts) {
            if (_retryDelay <= TimeSpan.Zero)
                return TimeSpan.Zero;

            int maxMultiplier = _retryMultipliers.Length > 0 ? _retryMultipliers.Last() : 1;
            int multiplier = attempts <= _retryMultipliers.Length ? _retryMultipliers[attempts - 1] : maxMultiplier;
            return TimeSpan.FromMilliseconds(_retryDelay.TotalMilliseconds * multiplier);
        }

        public IEnumerable<T> GetDeadletterItems() {
            throw new NotImplementedException();
        }

        public void DeleteQueue() {
            Log.Trace().Message("Deleting queue: {0}", _queueName).Write();
            DeleteList(QueueListName);
            DeleteList(WorkListName);
            DeleteList(WaitListName);
            DeleteList(DeadListName);
            _enqueuedCount = 0;
            _dequeuedCount = 0;
            _completedCount = 0;
            _abandonedCount = 0;
            _workerErrorCount = 0;
        }

        private void DeleteList(string name) {
            var itemIds = _db.ListRange(name);
            foreach (var id in itemIds) {
                _db.KeyDelete(GetPayloadKey(id));
                _db.KeyDelete(GetAttemptsKey(id));
                _db.KeyDelete(GetDequeuedTimeKey(id));
                _db.KeyDelete(GetWaitTimeKey(id));
                _lockProvider.ReleaseLock(id);
            }
            _db.KeyDelete(name);
        }

        private void OnTopicMessage(RedisChannel redisChannel, RedisValue redisValue) {
            Log.Trace().Message("Queue OnMessage {0}: {1}", _queueName, redisValue).Write();
            _autoEvent.Set();
        }

        private async Task WorkerLoop(CancellationToken token) {
            Log.Trace().Message("WorkerLoop Start {0}", _queueName).Write();
            while (!token.IsCancellationRequested && _workerAction != null) {
                Log.Trace().Message("WorkerLoop Pass {0}", _queueName).Write();
                QueueEntry<T> queueEntry = null;
                try {
                    queueEntry = Dequeue();
                } catch (TimeoutException) { }

                if (token.IsCancellationRequested || queueEntry == null)
                    continue;

                try {
                    _workerAction(queueEntry);
                    if (_workerAutoComplete)
                        queueEntry.Complete();
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Worker error: {0}", ex.Message).Write();
                    queueEntry.Abandon();
                    Interlocked.Increment(ref _workerErrorCount);
                }
            }

            Log.Trace().Message("Worker exiting: {0} Cancel Requested: {1}", _queueName, token.IsCancellationRequested).Write();
        }

        private async Task DoMaintenanceWork() {
            Log.Trace().Message("DoMaintenance {0}", _queueName).Write();
            var workIds = _db.ListRange(WorkListName);
            foreach (var workId in workIds) {
                var dequeuedTime = _cache.Get<DateTime?>(GetDequeuedTimeKey(workId));

                // dequeue time should be set, use current time
                if (!dequeuedTime.HasValue) {
                    _cache.Set(GetDequeuedTimeKey(workId), DateTime.UtcNow, GetDequeuedTimeTtl());
                    continue;
                }

                Log.Trace().Message("Dequeue time {0}", dequeuedTime).Write();
                if (DateTime.UtcNow.Subtract(dequeuedTime.Value) <= _workItemTimeout)
                    continue;

                Log.Trace().Message("Getting work time out lock...").Write();
                try {
                    using (_lockProvider.AcquireLock(workId, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5))) {
                        Log.Trace().Message("Got item lock for work time out").Write();
                        Abandon(workId);
                    }
                } catch {}
            }

            var waitIds = _db.ListRange(WaitListName);
            foreach (var waitId in waitIds) {
                var waitTime = _cache.Get<DateTime?>(GetWaitTimeKey(waitId));
                Log.Trace().Message("Wait time: {0}", waitTime).Write();

                if (waitTime.HasValue && waitTime.Value > DateTime.UtcNow)
                    continue;

                Log.Trace().Message("Getting retry lock").Write();

                try {
                    using (_lockProvider.AcquireLock(waitId, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5))) {
                        Log.Trace().Message("Adding item back to queue for retry: {0}", waitId).Write();
                        var tx = _db.CreateTransaction();
                        tx.ListRemoveAsync(WaitListName, waitId);
                        tx.ListLeftPushAsync(QueueListName, waitId);
                        var success = tx.Execute();
                        if (!success)
                            throw new Exception("Unable to move item to queue list.");

                        _db.KeyDelete(GetWaitTimeKey(waitId));
                        _subscriber.Publish(GetTopicName(), waitId);
                    }
                } catch {}
            }
        }

        public void Dispose() {
            Log.Trace().Message("Queue {0} dispose", _queueName).Write();
            StopWorking();
            if (_queueDisposedCancellationTokenSource != null)
                _queueDisposedCancellationTokenSource.Cancel();
        }
    }
}
