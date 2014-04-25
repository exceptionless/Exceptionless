using System;

namespace Exceptionless.Core.Messaging {
    public interface IMessageSubscriber {
        void Subscribe<T>(Action<T> handler) where T : class;
    }
}
