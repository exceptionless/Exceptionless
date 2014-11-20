using System;
using System.Threading.Tasks;

namespace Exceptionless.Core.Queues {
    public class QueueEntry<T> : IDisposable where T: class {
        private readonly IQueue<T> _queue;
        private bool _isCompleted;

        public QueueEntry(string id, T value, IQueue<T> queue) {
            Id = id;
            Value = value;
            _queue = queue;
        }

        public string Id { get; private set; }

        public T Value { get; private set; }

        public virtual Task CompleteAsync() {
            _isCompleted = true;
            return _queue.CompleteAsync(Id);
        }

        public virtual Task AbandonAsync() {
            return _queue.AbandonAsync(Id);
        }

        public virtual void Dispose() {
            if (!_isCompleted)
                AbandonAsync().Wait();
        }
    }
}