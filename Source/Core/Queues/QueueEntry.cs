using System;

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

        public void Complete() {
            if (_isCompleted)
                return;

            _isCompleted = true;
            _queue.Complete(Id);
        }

        public void Abandon() {
            _queue.Abandon(Id);
        }

        public virtual void Dispose() {
            if (!_isCompleted)
                Abandon();
        }
    }
}