using System;
using System.Threading.Tasks;

namespace Exceptionless.Core.Queues {
    public interface IQueue<T> {
        Task EnqueueAsync(T data);
        Task<WorkItem<T>> DequeueAsync();
        Task CompleteAsync(string id);
        Task AbandonAsync(string id);
    }
}
