using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using NLog.Fluent;

namespace Exceptionless.Core.Messaging {
    public class ServiceBusMessageBus : IMessagePublisher, IMessageSubscriber {
        private readonly string _connectionString;
        private readonly string _topicName;
        private readonly string _subscriptionName;
        private readonly NamespaceManager _namespaceManager;
        private readonly TopicClient _topicClient;
        private readonly SubscriptionClient _subscriptionClient;
        private readonly BlockingCollection<Subscriber> _subscribers = new BlockingCollection<Subscriber>();

        public ServiceBusMessageBus(string connectionString, string topicName) {
            _topicName = topicName;
            _subscriptionName = "MessageBus";
            _connectionString = connectionString;
            _namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
            if (!_namespaceManager.TopicExists(_topicName))
                _namespaceManager.CreateTopic(_topicName);

            _topicClient = TopicClient.CreateFromConnectionString(connectionString, _topicName);
            if (!_namespaceManager.SubscriptionExists(_topicName, _subscriptionName))
                _namespaceManager.CreateSubscription(_topicName, _subscriptionName);

            _subscriptionClient = SubscriptionClient.CreateFromConnectionString(connectionString, _topicName, _subscriptionName, ReceiveMode.ReceiveAndDelete);
            _subscriptionClient.OnMessageAsync(OnMessage);
        }

        private Task OnMessage(BrokeredMessage brokeredMessage) {
            var message = brokeredMessage.GetBody<MessageBusData>();

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
            brokeredMessage.Complete();

            return Task.FromResult(0);
        }

        public Task PublishAsync<T>(T message) where T : class {
            return _topicClient.SendAsync(new BrokeredMessage(new MessageBusData { Type = typeof(T).AssemblyQualifiedName, Data = message.ToJson() }));
        }

        public void Subscribe<T>(Action<T> handler) where T : class {
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
