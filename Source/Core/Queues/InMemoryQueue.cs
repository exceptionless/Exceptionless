using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Component;
using NLog.Fluent;

namespace Exceptionless.Core.Queues {
    public class InMemoryQueue<T> : IQueue<T>, IDisposable where T : class {
        private readonly ConcurrentQueue<QueueInfo<T>> _queue = new ConcurrentQueue<QueueInfo<T>>();
        private readonly ConcurrentDictionary<string, QueueInfo<T>> _dequeued = new ConcurrentDictionary<string, QueueInfo<T>>();
        private readonly ConcurrentQueue<QueueInfo<T>> _deadletterQueue = new ConcurrentQueue<QueueInfo<T>>();
        private readonly AutoResetEvent _autoEvent = new AutoResetEvent(false);
        private Action<QueueEntry<T>> _workerAction;
        private bool _workerAutoComplete;
        private readonly TimeSpan _workItemTimeout = TimeSpan.FromMinutes(10);
        private readonly TimeSpan _retryDelay = TimeSpan.FromMinutes(1);
        private readonly int[] _retryMultipliers = { 1, 3, 5, 10 };
        private readonly int _retries = 2;
        private int _enqueuedCount;
        private int _dequeuedCount;
        private int _completedCount;
        private int _abandonedCount;
        private int _workerErrorCount;
        private CancellationTokenSource _workerCancellationTokenSource;
        private readonly CancellationTokenSource _queueDisposedCancellationTokenSource;

        public InMemoryQueue(int retries = 2, TimeSpan? retryDelay = null, int[] retryMultipliers = null, TimeSpan? workItemTimeout = null) {
            QueueId = Guid.NewGuid().ToString("N");
            _retries = retries;
            if (retryDelay.HasValue)
                _retryDelay = retryDelay.Value;
            if (retryMultipliers != null)
                _retryMultipliers = retryMultipliers;
            if (workItemTimeout.HasValue)
                _workItemTimeout = workItemTimeout.Value;

            _queueDisposedCancellationTokenSource = new CancellationTokenSource();
            TaskHelper.RunPeriodic(DoMaintenance, _workItemTimeout > TimeSpan.FromSeconds(1) ? _workItemTimeout : TimeSpan.FromSeconds(1), _queueDisposedCancellationTokenSource.Token, TimeSpan.FromMilliseconds(100));
        }

        public long GetQueueCount() { return _queue.Count; }
        public long GetWorkingCount() { return _dequeued.Count; }
        public long GetDeadletterCount() { return _deadletterQueue.Count; }

        public long EnqueuedCount { get { return _enqueuedCount; } }
        public long DequeuedCount { get { return _dequeuedCount; } }
        public long CompletedCount { get { return _completedCount; } }
        public long AbandonedCount { get { return _abandonedCount; } }
        public long WorkerErrorCount { get { return _workerErrorCount; } }
        public string QueueId { get; private set; }

        public string Enqueue(T data) {
            string id = Guid.NewGuid().ToString("N");
            Log.Trace().Message("Queue {0} enqueue item: {1}", typeof(T).Name, id).Write();
            var info = new QueueInfo<T> {
                Data = data,
                Id = id
            };
            _queue.Enqueue(info);
            Log.Trace().Message("Enqueue: Set Event").Write();
            _autoEvent.Set();
            Interlocked.Increment(ref _enqueuedCount);
            Log.Trace().Message("Enqueue done").Write();

            return id;
        }

        public void StartWorking(Action<QueueEntry<T>> handler, bool autoComplete = false) {
            if (handler == null)
                throw new ArgumentNullException("handler");

            Log.Trace().Message("Queue {0} start working", typeof(T).Name).Write();
            _workerAction = handler;
            _workerAutoComplete = autoComplete;
            if (_workerCancellationTokenSource != null)
                return;

            _workerCancellationTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(() => WorkerLoop(_workerCancellationTokenSource.Token));
        }

        public void StopWorking() {
            Log.Trace().Message("Queue {0} stop working", typeof(T).Name).Write();
            if (_workerCancellationTokenSource != null)
                _workerCancellationTokenSource.Cancel();

            _workerCancellationTokenSource = null;
            _workerAction = null;
        }

        public QueueEntry<T> Dequeue(TimeSpan? timeout = null) {
            Log.Trace().Message("Queue {0} dequeued item", typeof(T).Name).Write();
            if (!timeout.HasValue)
                timeout = TimeSpan.FromSeconds(30);

            Log.Trace().Message("Queue count: {0}", _queue.Count).Write();
            _autoEvent.WaitOne(timeout.Value);
            if (_queue.Count == 0)
                return null;

            Log.Trace().Message("Dequeue: Attempt").Write();
            QueueInfo<T> info;
            if (!_queue.TryDequeue(out info) || info == null)
                return null;

            Log.Trace().Message("Dequeue: Got Item").Write();
            Interlocked.Increment(ref _dequeuedCount);

            info.Attempts++;
            info.TimeDequeued = DateTime.Now;

            if (!_dequeued.TryAdd(info.Id, info))
                throw new ApplicationException("Unable to add item to the dequeued list.");

            return new QueueEntry<T>(info.Id, info.Data, this);
        }

        public void Complete(string id) {
            Log.Trace().Message("Queue {0} complete item: {1}", typeof(T).Name, id).Write();
            QueueInfo<T> info = null;
            if (!_dequeued.TryRemove(id, out info) || info == null)
                throw new ApplicationException("Unable to remove item from the dequeued list.");

            Interlocked.Increment(ref _completedCount);
            Log.Trace().Message("Complete done: {0}", id).Write();
        }

        public void Abandon(string id) {
            Log.Trace().Message("Queue {0} abandon item: {1}", typeof(T).Name, id).Write();
            QueueInfo<T> info;
            if (!_dequeued.TryRemove(id, out info) || info == null)
                throw new ApplicationException("Unable to remove item from the dequeued list.");

            Interlocked.Increment(ref _abandonedCount);
            if (info.Attempts < _retries + 1) {
                if (_retryDelay > TimeSpan.Zero) {
                    Log.Trace().Message("Adding item to wait list for future retry: {0}", id).Write();
                    Task.Factory.StartNewDelayed(GetRetryDelay(info.Attempts), () => Retry(info));
                } else {
                    Log.Trace().Message("Adding item back to queue for retry: {0}", id).Write();
                    Retry(info);
                }
            } else {
                Log.Trace().Message("Exceeded retry limit moving to deadletter: {0}", id).Write();
                _deadletterQueue.Enqueue(info);
            }
            Log.Trace().Message("Abondon complete: {0}", id).Write();
        }

        private void Retry(QueueInfo<T> info) {
            Trace.WriteLine("Retry");
            _queue.Enqueue(info);
            Trace.WriteLine("Retry: Set Event");
            _autoEvent.Set();
        }

        private int GetRetryDelay(int attempts) {
            int maxMultiplier = _retryMultipliers.Length > 0 ? _retryMultipliers.Last() : 1;
            int multiplier = attempts <= _retryMultipliers.Length ? _retryMultipliers[attempts - 1] : maxMultiplier;
            return (int)(_retryDelay.TotalMilliseconds * multiplier);
        }

        public IEnumerable<T> GetDeadletterItems() {
            return _deadletterQueue.Select(i => i.Data);
        }

        public void DeleteQueue() {
            Log.Trace().Message("Deleting queue: {0}", typeof(T).Name).Write();
            _queue.Clear();
            _deadletterQueue.Clear();
            _dequeued.Clear();
            _enqueuedCount = 0;
            _dequeuedCount = 0;
            _completedCount = 0;
            _abandonedCount = 0;
            _workerErrorCount = 0;
        }

        private async Task WorkerLoop(CancellationToken token) {
            Log.Trace().Message("WorkerLoop Start {0}", typeof(T).Name).Write();
            while (!token.IsCancellationRequested) {
                if (_queue.Count == 0 || _workerAction == null)
                    _autoEvent.WaitOne(TimeSpan.FromMilliseconds(250));

                Log.Trace().Message("WorkerLoop Signaled {0}", typeof(T).Name).Write();
                QueueEntry<T> queueEntry = null;
                try {
                    queueEntry = Dequeue(TimeSpan.Zero);
                } catch (TimeoutException) { }

                if (queueEntry == null || _workerAction == null)
                    return;

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
        }

        private async Task DoMaintenance() {
            Log.Trace().Message("DoMaintenance {0}", typeof(T).Name).Write();
            foreach (var item in _dequeued.Where(kvp => DateTime.Now.Subtract(kvp.Value.TimeDequeued).Milliseconds > _workItemTimeout.TotalMilliseconds)) {
                Log.Trace().Message("DoMaintenance Abandon: {0}", item.Key).Write();
                Abandon(item.Key);
            }
        }

        public void Dispose() {
            Log.Trace().Message("Queue {0} dispose", typeof(T).Name).Write();
            StopWorking();
            if (_queueDisposedCancellationTokenSource != null)
                _queueDisposedCancellationTokenSource.Cancel();
        }

        private class QueueInfo<T> {
            public T Data { get; set; }
            public string Id { get; set; }
            public int Attempts { get; set; }
            public DateTime TimeDequeued { get; set; }
        }
    }
}
