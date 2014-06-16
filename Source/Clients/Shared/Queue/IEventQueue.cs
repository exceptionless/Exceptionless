using System;
using System.Threading.Tasks;
using Exceptionless.Models;

namespace Exceptionless.Queue {
    public interface IEventQueue : IDisposable {
        void Enqueue(Event ev);
        Task ProcessAsync();
        void SuspendProcessing(TimeSpan? duration = null, bool discardFutureQueuedItems = false, bool clearQueue = false);
    }
}