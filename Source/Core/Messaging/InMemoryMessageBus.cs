using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using NLog.Fluent;

namespace Exceptionless.Core.Messaging {
    public class InMemoryMessageBus : IMessagePublisher, IMessageSubscriber {
        private readonly BlockingCollection<Subscriber> _subscribers = new BlockingCollection<Subscriber>();

        public void Publish<T>(T message) where T: class {
            if (message == null)
                throw new ArgumentNullException("message");

            Task.Factory.StartNew(() => {
                foreach (var subscriber in _subscribers.Where(s => s.Type.IsAssignableFrom(typeof(T))).ToList()) {
                    try {
                        subscriber.Action(message);
                    } catch (Exception ex) {
                        Log.Error().Exception(ex).Message("Error sending message to subscriber: {0}", ex.Message).Write();
                    }
                }
            });
        }

        public void Subscribe<T>(Action<T> handler) where T: class {
            _subscribers.Add(new Subscriber {
                Type = typeof(T),
                Action = m => {
                    if (!(m is T))
                        return;

                    handler(m as T);
                }
            });
        }

        private class Subscriber {
            public Type Type { get; set; }
            public Action<object> Action { get; set; }
        }
    }
}
