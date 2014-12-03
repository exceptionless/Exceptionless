using System;
using System.Collections.Concurrent;
using System.Linq;
using Exceptionless.Core.Extensions;
using NLog.Fluent;
using StackExchange.Redis;

namespace Exceptionless.Core.Messaging {
    public class RedisMessageBus : IMessagePublisher, IMessageSubscriber {
        private readonly ISubscriber _subscriber;
        private readonly BlockingCollection<Subscriber> _subscribers = new BlockingCollection<Subscriber>();
        private readonly string _topic;

        public RedisMessageBus(ISubscriber subscriber, string topic = null) {
            _subscriber = subscriber;
            _topic = topic ?? "messages";
            _subscriber.Subscribe(_topic, OnMessage);
        }

        private void OnMessage(RedisChannel channel, RedisValue value) {
            var message = ((string)value).FromJson<MessageBusData>();

            Type messageType = null;
            try {
                messageType = Type.GetType(message.Type);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error getting message body type: {0}", ex.Message).Write();
            }

            object body = message.Data.FromJson(messageType);
            foreach (var subscriber in _subscribers.Where(s => s.Type.IsAssignableFrom(messageType)).ToList()) {
                try {
                    subscriber.Action(body);
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Error sending message to subscriber: {0}", ex.Message).Write();
                }
            }
        }

        public void Publish<T>(T message) where T: class {
            if (message == null)
                throw new ArgumentNullException("message");

            _subscriber.Publish(_topic, new MessageBusData { Type = typeof(T).AssemblyQualifiedName, Data = message.ToJson() }.ToJson());
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
