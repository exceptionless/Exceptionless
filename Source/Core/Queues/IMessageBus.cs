using System;

namespace Exceptionless.Core.Queues {
    public interface IMessageBus {
        void Publish<T>(T message);
        void Subscribe<T>(Func<T> handler);
    }
}
