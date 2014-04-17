using System;
using System.Threading.Tasks;
using Exceptionless.Models;

namespace Exceptionless.Queue {
    interface IEventQueue {
        void Enqueue(Event ev);
        Task ProcessAsync();
        Configuration Configuration { get; set; }
    }
}
