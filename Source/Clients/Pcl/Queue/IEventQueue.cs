using System;
using System.Threading.Tasks;
using Exceptionless.Models;

namespace Exceptionless.Queue {
    public interface IEventQueue : IDisposable {
        Task EnqueueAsync(Event ev);
        Task ProcessAsync(TimeSpan? delay = null);
        void SuspendProcessing(TimeSpan? duration = null, bool discardFutureQueuedItems = false, bool clearQueue = false);
    }
}