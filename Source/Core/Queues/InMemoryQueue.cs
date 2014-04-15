using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Exceptionless.Core.Queues {
    public class InMemoryQueue<T> : IQueue<T> {
        private readonly ConcurrentQueue<QueueInfo<T>> _queue = new ConcurrentQueue<QueueInfo<T>>();
        private readonly ConcurrentDictionary<string, QueueInfo<T>> _dequeued = new ConcurrentDictionary<string, QueueInfo<T>>();
        private readonly AutoResetEvent _autoEvent = new AutoResetEvent(false);

        public Task EnqueueAsync(T data) {
            _queue.Enqueue(new QueueInfo<T> { Data = data, Id = Guid.NewGuid().ToString() });
            _autoEvent.Set();
            return Task.FromResult(0);
        }

        public Task<WorkItem<T>> DequeueAsync() {
            try {
                if (_queue.Count == 0 && !_autoEvent.WaitOne(TimeSpan.FromSeconds(30)))
                    throw new TimeoutException();

                QueueInfo<T> info;
                if (!_queue.TryDequeue(out info) || info == null)
                    throw new TimeoutException();

                if (!_dequeued.TryAdd(info.Id, info))
                    throw new ApplicationException("Unable to add item to the dequeued list.");

                return Task.FromResult(new WorkItem<T>(info.Id, info.Data, this));
            } catch (Exception ex) {
                var completionSource = new TaskCompletionSource<WorkItem<T>>();
                completionSource.SetException(ex);
                return completionSource.Task;
            }
        }

        public Task CompleteAsync(string id) {
            QueueInfo<T> info = null;
            if (!_dequeued.TryRemove(id, out info) || info == null)
                throw new ApplicationException("Unable to remove item from the dequeued list.");

            return Task.FromResult(0);
        }

        public Task AbandonAsync(string id) {
            QueueInfo<T> info = null;
            if (!_dequeued.TryRemove(id, out info) || info == null)
                throw new ApplicationException("Unable to remove item from the dequeued list.");

            info.Attempts++;
            _queue.Enqueue(info);

            return Task.FromResult(0);
        }

        private class QueueInfo<T> {
            public T Data { get; set; }
            public string Id { get; set; }
            public int Attempts { get; set; }
    }
    }
}
