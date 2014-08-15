using System;
using System.Threading.Tasks;

namespace Exceptionless.Core.Messaging {
    public interface IMessagePublisher {
        Task PublishAsync<T>(T message) where T : class;
    }
}
