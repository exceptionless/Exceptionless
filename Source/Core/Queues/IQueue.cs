using System;
using System.Threading.Tasks;

namespace Exceptionless.Core.Queues {
    public interface IQueue<T> where T : class {
        Task EnqueueAsync(T data);
        void StartWorking(Action<WorkItem<T>> handler, bool autoComplete = false);
        Task<WorkItem<T>> DequeueAsync(int millisecondsTimeout = 30 * 1000);
        Task CompleteAsync(string id);
        Task AbandonAsync(string id);
    }
}
