using System;
using System.Threading.Tasks;

namespace Exceptionless.Core.Queues {
    public interface IQueue<T> {
        Task EnqueueAsync(T data);
        Task<WorkItem<T>> DequeueAsync();
        Task CompleteAsync(string id);
        Task AbandonAsync(string id);
    }

    public class WorkItem<T> : IDisposable {
        private readonly IQueue<T> _queue; 
        public WorkItem(string id, T value, IQueue<T> queue) {
            Id = id;
            Value = value;
            _queue = queue;
        }

        public string Id { get; private set; }

        public T Value { get; private set; }

        public Task CompleteAsync() {
            return _queue.CompleteAsync(Id);
        }

        public Task AbandonAsync() {
            return _queue.AbandonAsync(Id);
        }

        public void Dispose() {
        }
    }
}
