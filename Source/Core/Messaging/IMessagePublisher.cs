using System;

namespace Exceptionless.Core.Messaging {
    public interface IMessagePublisher {
        void Publish<T>(T message) where T : class;
    }
}
