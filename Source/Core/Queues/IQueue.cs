using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Exceptionless.Core.Queues {
    public interface IQueue<T> : IDisposable where T : class {
        Task EnqueueAsync(T data);
        void StartWorking(Action<QueueEntry<T>> handler, bool autoComplete = false);
        void StartWorking(Func<QueueEntry<T>, Task> handler, bool autoComplete = false);
        void StopWorking();
        Task<QueueEntry<T>> DequeueAsync(TimeSpan? timeout = null);
        Task CompleteAsync(string id);
        Task AbandonAsync(string id);
        Task<long> GetQueueCountAsync();
        Task<long> GetWorkingCountAsync();
        Task<long> GetDeadletterCountAsync();
        Task<IEnumerable<T>> GetDeadletterItemsAsync();
        Task ResetQueueAsync();
        long EnqueuedCount { get; }
        long DequeuedCount { get; }
        long CompletedCount { get; }
        long AbandonedCount { get; }
        long WorkerErrorCount { get; }
    }
}
