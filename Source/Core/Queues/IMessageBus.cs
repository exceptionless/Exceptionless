using System;
using System.Threading.Tasks;

namespace Exceptionless.Core.Queues {
    public interface IMessageBus {
        Task PublishAsync<T>(T message);
        void Subscribe<T>(Action<T> handler);
    }
}
