using System;

namespace Exceptionless.Core.Messaging {
    public interface IMessageBus : IMessagePublisher, IMessageSubscriber { }
}