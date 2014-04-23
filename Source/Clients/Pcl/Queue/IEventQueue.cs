using System;
using System.Threading.Tasks;
using Exceptionless.Models;

namespace Exceptionless.Queue {
    public interface IEventQueue {
        Task EnqueueAsync(Event ev);
        Task ProcessAsync();
   }
}