using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Extensions;
using NLog.Fluent;

namespace Exceptionless.Core.Queues {
    public class InMemoryQueue<T> : IQueue<T>, IDisposable where T : class {
        private readonly ConcurrentQueue<QueueInfo<T>> _queue = new ConcurrentQueue<QueueInfo<T>>();
        private readonly ConcurrentDictionary<string, QueueInfo<T>> _dequeued = new ConcurrentDictionary<string, QueueInfo<T>>();
        private readonly ConcurrentQueue<QueueInfo<T>> _deadletterQueue = new ConcurrentQueue<QueueInfo<T>>();
        private readonly AutoResetEvent _autoEvent = new AutoResetEvent(false);
        private readonly BlockingCollection<Worker> _workers = new BlockingCollection<Worker>();
        private readonly int _timeoutMilliseconds;
        private readonly int _retryDelayMilliseconds;
        private readonly int _retries;
        private int _enqueuedCounter = 0;
        private int _dequeuedCounter = 0;
        private int _completedCounter = 0;
        private int _abandonedCounter = 0;
        private int _workerErrorsCounter = 0;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public InMemoryQueue(int retries = 2, int workItemTimeoutMilliseconds = 60 * 1000, int retryDelayMilliseconds = 1000) {
            _retries = retries;
            _timeoutMilliseconds = workItemTimeoutMilliseconds;
            _retryDelayMilliseconds = retryDelayMilliseconds;
            Task.Factory.StartNew(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested) {
                    _dequeued.Where(kvp => DateTime.Now.Subtract(kvp.Value.TimeDequeued).Milliseconds > _timeoutMilliseconds).ForEach(kvp => AbandonAsync(kvp.Key));
                    await Task.Delay(Math.Min(500, workItemTimeoutMilliseconds), _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);
        }

        public Task EnqueueAsync(T data) {
            var info = new QueueInfo<T> {
                Data = data,
                Id = Guid.NewGuid().ToString()
            };
            _queue.Enqueue(info);
            _autoEvent.Set();
            Interlocked.Increment(ref _enqueuedCounter);

            RunWorkersAsync();

            return Task.FromResult(0);
        }

        private Task RunWorkersAsync() {
            return Task.Factory.StartNew(() => {
                if (_workers.Count == 0)
                    return;

                QueueEntry<T> queueEntry = null;
                try {
                    queueEntry = DequeueAsync(0).Result;
                } catch (TimeoutException) {}
                if (queueEntry == null)
                    return;

                // get a random worker
                var worker = _workers.ToList().Random();
                if (worker == null)
                    return;
                try {
                    worker.Action(queueEntry);
                    if (worker.AutoComplete)
                        queueEntry.CompleteAsync().Wait();
                } catch (Exception ex) {
                    Interlocked.Increment(ref _workerErrorsCounter);
                    Log.Error().Exception(ex).Message("Error sending work item to worker: {0}", ex.Message).Write();
                    queueEntry.AbandonAsync().Wait();
                }
            });
        }

        public void StartWorking(Action<QueueEntry<T>> handler, bool autoComplete = false) {
            _workers.Add(new Worker { Action = handler, AutoComplete = autoComplete });
        }

        public Task<QueueEntry<T>> DequeueAsync(int millisecondsTimeout = 30 * 1000) {
            try {
                if (_queue.Count == 0 && !_autoEvent.WaitOne(millisecondsTimeout))
                    return Task.FromResult<QueueEntry<T>>(null);

                QueueInfo<T> info;
                if (!_queue.TryDequeue(out info) || info == null)
                    return Task.FromResult<QueueEntry<T>>(null);

                Interlocked.Increment(ref _dequeuedCounter);

                info.Attempts++;
                info.TimeDequeued = DateTime.Now;

                if (!_dequeued.TryAdd(info.Id, info))
                    throw new ApplicationException("Unable to add item to the dequeued list.");

                return Task.FromResult(new QueueEntry<T>(info.Id, info.Data, this));
            } catch (Exception ex) {
                var completionSource = new TaskCompletionSource<QueueEntry<T>>();
                completionSource.SetException(ex);
                return completionSource.Task;
            }
        }

        public int Count { get { return _queue.Count; } }
        public int DeadletterCount { get { return _deadletterQueue.Count; } }
        public int Enqueued { get { return _enqueuedCounter; } }
        public int Dequeued { get { return _dequeuedCounter; } }
        public int Completed { get { return _completedCounter; } }
        public int Abandoned { get { return _abandonedCounter; } }
        public int WorkerErrors { get { return _workerErrorsCounter; } }

        public Task CompleteAsync(string id) {
            QueueInfo<T> info = null;
            if (!_dequeued.TryRemove(id, out info) || info == null)
                throw new ApplicationException("Unable to remove item from the dequeued list.");

            Interlocked.Increment(ref _completedCounter);
            return Task.FromResult(0);
        }

        public Task AbandonAsync(string id) {
            QueueInfo<T> info = null;
            if (!_dequeued.TryRemove(id, out info) || info == null)
                throw new ApplicationException("Unable to remove item from the dequeued list.");

            Interlocked.Increment(ref _abandonedCounter);
            if (info.Attempts < _retries + 1) {
                if (_retryDelayMilliseconds > 0)
                    Task.Factory.StartNewDelayed(_retryDelayMilliseconds, () => Retry(info));
                else
                    Retry(info);
            } else {
                _deadletterQueue.Enqueue(info);
            }

            return Task.FromResult(0);
        }

        private void Retry(QueueInfo<T> info) {
            _queue.Enqueue(info);
            RunWorkersAsync();
        }

        public void Dispose() {
            _cancellationTokenSource.Cancel();
        }

        private class QueueInfo<T> {
            public T Data { get; set; }
            public string Id { get; set; }
            public int Attempts { get; set; }
            public DateTime TimeDequeued { get; set; }
        }

        private class Worker {
            public bool AutoComplete { get; set; }
            public Action<QueueEntry<T>> Action { get; set; }
        }
    }
}
