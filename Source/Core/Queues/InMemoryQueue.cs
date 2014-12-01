using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Component;
using Exceptionless.Core.Extensions;
using Nito.AsyncEx;
using NLog.Fluent;

namespace Exceptionless.Core.Queues {
    public class InMemoryQueue<T> : IQueue<T>, IDisposable where T : class {
        private readonly ConcurrentQueue<QueueInfo<T>> _queue = new ConcurrentQueue<QueueInfo<T>>();
        private readonly ConcurrentDictionary<string, QueueInfo<T>> _dequeued = new ConcurrentDictionary<string, QueueInfo<T>>();
        private readonly ConcurrentQueue<QueueInfo<T>> _deadletterQueue = new ConcurrentQueue<QueueInfo<T>>();
        private readonly AsyncAutoResetEvent _autoEvent = new AsyncAutoResetEvent(false);
        private Func<QueueEntry<T>, Task> _workerAction;
        private bool _workerAutoComplete;
        private readonly TimeSpan _workItemTimeout = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(1);
        private readonly int _retries;
        private int _enqueuedCount = 0;
        private int _dequeuedCount = 0;
        private int _completedCount = 0;
        private int _abandonedCount = 0;
        private int _workerErrorCount = 0;
        private CancellationTokenSource _workerCancellationTokenSource;
        private CancellationTokenSource _queueDisposedCancellationTokenSource;

        public InMemoryQueue(int retries = 2, TimeSpan? workItemTimeout = null, TimeSpan? retryDelay = null) {
            _retries = retries;
            if (workItemTimeout.HasValue)
                _workItemTimeout = workItemTimeout.Value;
            if (retryDelay.HasValue)
                _retryDelay = retryDelay.Value;

            _queueDisposedCancellationTokenSource = new CancellationTokenSource();
            TaskHelper.RunPeriodic(DoMaintenance, _workItemTimeout > TimeSpan.FromSeconds(1) ? _workItemTimeout : TimeSpan.FromSeconds(1), _queueDisposedCancellationTokenSource.Token, TimeSpan.FromMilliseconds(100));
        }

        private async Task DoMaintenance() {
            Trace.WriteLine("DoMaintenance: " + Thread.CurrentThread.ManagedThreadId);
            foreach (var item in _dequeued.Where(kvp => DateTime.Now.Subtract(kvp.Value.TimeDequeued).Milliseconds > _workItemTimeout.TotalMilliseconds)) {
                Trace.WriteLine("DoMaintenance: Abandon " + item.Key);
                await AbandonAsync(item.Key);
            }
        }

        public Task EnqueueAsync(T data) {
            Trace.WriteLine("Enqueue");
            var info = new QueueInfo<T> {
                Data = data,
                Id = Guid.NewGuid().ToString()
            };
            _queue.Enqueue(info);
            Trace.WriteLine("Enqueue: Set Event");
            _autoEvent.Set();
            Interlocked.Increment(ref _enqueuedCount);

            return Task.FromResult(0);
        }

        private async Task WorkerLoop(CancellationToken token) {
            Trace.WriteLine("WorkerLoop: " + Thread.CurrentThread.ManagedThreadId);
            while (!token.IsCancellationRequested) {
                if (_queue.Count == 0 || _workerAction == null)
                    await _autoEvent.WaitAsync(token);

                Debug.WriteLine("WorkerLoop");
                QueueEntry<T> queueEntry = null;
                try {
                    queueEntry = await DequeueAsync(TimeSpan.Zero);
                } catch (TimeoutException) { }

                if (queueEntry == null || _workerAction == null)
                    return;

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

            _workerCancellationTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(() => WorkerLoop(_workerCancellationTokenSource.Token));
        }

        public void StopWorking() {
            if (_workerCancellationTokenSource != null)
                _workerCancellationTokenSource.Cancel();

            _workerCancellationTokenSource = null;
            _workerAction = null;
        }

        public async Task<QueueEntry<T>> DequeueAsync(TimeSpan? timeout = null) {
            if (!timeout.HasValue)
                timeout = TimeSpan.FromSeconds(30);

            Trace.WriteLine("Dequeue Count: " + _queue.Count);
            await _autoEvent.WaitAsync(timeout.Value);
            if (_queue.Count == 0)
                return null;

            Trace.WriteLine("Dequeue: Attempt");
            QueueInfo<T> info;
            if (!_queue.TryDequeue(out info) || info == null)
                return null;

            Trace.WriteLine("Dequeue: Got Item");
            Interlocked.Increment(ref _dequeuedCount);

            info.Attempts++;
            info.TimeDequeued = DateTime.Now;

            if (!_dequeued.TryAdd(info.Id, info))
                throw new ApplicationException("Unable to add item to the dequeued list.");

            return new QueueEntry<T>(info.Id, info.Data, this);
        }

        public Task<long> GetQueueCountAsync() { return Task.FromResult((long)_queue.Count); }
        public Task<long> GetWorkingCountAsync() { return Task.FromResult((long)_dequeued.Count); }
        public Task<long> GetDeadletterCountAsync() { return Task.FromResult((long)_deadletterQueue.Count); }

        public Task<IEnumerable<T>> GetDeadletterItemsAsync() {
            return Task.FromResult(_deadletterQueue.Select(i => i.Data));
        }

        public Task ResetQueueAsync() {
            _queue.Clear();
            _deadletterQueue.Clear();
            _dequeued.Clear();
            _enqueuedCount = 0;
            _dequeuedCount = 0;
            _completedCount = 0;
            _abandonedCount = 0;
            _workerErrorCount = 0;

            return Task.FromResult(0);
        }

        public long EnqueuedCount { get { return _enqueuedCount; } }
        public long DequeuedCount { get { return _dequeuedCount; } }
        public long CompletedCount { get { return _completedCount; } }
        public long AbandonedCount { get { return _abandonedCount; } }
        public long WorkerErrorCount { get { return _workerErrorCount; } }

        public Task CompleteAsync(string id) {
            QueueInfo<T> info = null;
            if (!_dequeued.TryRemove(id, out info) || info == null)
                throw new ApplicationException("Unable to remove item from the dequeued list.");

            Interlocked.Increment(ref _completedCount);
            return Task.FromResult(0);
        }

        public Task AbandonAsync(string id) {
            Trace.WriteLine("Abandon");
            QueueInfo<T> info = null;
            if (!_dequeued.TryRemove(id, out info) || info == null)
                throw new ApplicationException("Unable to remove item from the dequeued list.");

            Trace.WriteLine("Abandon: Removed");
            Interlocked.Increment(ref _abandonedCount);
            if (info.Attempts < _retries + 1) {
                Trace.WriteLine("Abandon: Retrying");
                if (_retryDelay > TimeSpan.Zero)
                    Task.Factory.StartNewDelayed((int)_retryDelay.TotalMilliseconds, () => Retry(info));
                else
                    Retry(info);
            } else {
                Trace.WriteLine("Abandon: Deadletter");
                _deadletterQueue.Enqueue(info);
            }

            return Task.FromResult(0);
        }

        private void Retry(QueueInfo<T> info) {
            Trace.WriteLine("Retry");
            _queue.Enqueue(info);
            Trace.WriteLine("Retry: Set Event");
            _autoEvent.Set();
        }

        public void Dispose() {
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
