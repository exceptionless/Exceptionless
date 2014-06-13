using System;
using System.Threading;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Messaging;
using Xunit;

namespace Exceptionless.Api.Tests.Messaging {
    public class ServiceBusMessagingTests {
        private static readonly Lazy<ServiceBusMessageBus> _messageBus = new Lazy<ServiceBusMessageBus>(() => {
            if (String.IsNullOrEmpty(Settings.Current.AzureServiceBusConnectionString))
                return null;

            return new ServiceBusMessageBus(Settings.Current.AzureServiceBusConnectionString, "test-messagebus");
        });

        [Fact(Skip = "Requires Azure Service Bus")]
        public void CanSendMessage() {
            var resetEvent = new AutoResetEvent(false);
            _messageBus.Value.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                resetEvent.Set();
            });
            _messageBus.Value.PublishAsync(new SimpleMessageA {
                Data = "Hello"
            });

            bool success = resetEvent.WaitOne(5000);
            Assert.True(success, "Failed to receive message.");
        }

        [Fact(Skip = "Requires Azure Service Bus")]
        public void CanSendMessageToMultipleSubscribers() {
            var latch = new CountDownLatch(3);
            _messageBus.Value.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            _messageBus.Value.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            _messageBus.Value.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            _messageBus.Value.PublishAsync(new SimpleMessageA {
                Data = "Hello"
            });

            bool success = latch.Wait(5000);
            Assert.True(success, "Failed to receive all messages.");
        }

        [Fact(Skip = "Requires Azure Service Bus")]
        public void CanTolerateSubscriberFailure() {
            var latch = new CountDownLatch(2);
            _messageBus.Value.Subscribe<SimpleMessageA>(msg => {
                throw new ApplicationException();
            });
            _messageBus.Value.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            _messageBus.Value.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            _messageBus.Value.PublishAsync(new SimpleMessageA {
                Data = "Hello"
            });

            bool success = latch.Wait(5000);
            Assert.True(success, "Failed to receive all messages.");
        }

        [Fact(Skip = "Requires Azure Service Bus")]
        public void WillOnlyReceiveSubscribedMessageType() {
            var resetEvent = new AutoResetEvent(false);
            _messageBus.Value.Subscribe<SimpleMessageB>(msg => {
                Assert.True(false, "Received wrong message type.");
            });
            _messageBus.Value.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                resetEvent.Set();
            });
            _messageBus.Value.PublishAsync(new SimpleMessageA {
                Data = "Hello"
            });

            bool success = resetEvent.WaitOne(5000);
            Assert.True(success, "Failed to receive message.");
        }

        [Fact(Skip = "Requires Azure Service Bus")]
        public void WillReceiveDerivedMessageTypes() {
            var latch = new CountDownLatch(2);
            _messageBus.Value.Subscribe<ISimpleMessage>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            _messageBus.Value.PublishAsync(new SimpleMessageA {
                Data = "Hello"
            });
            _messageBus.Value.PublishAsync(new SimpleMessageB {
                Data = "Hello"
            });
            _messageBus.Value.PublishAsync(new SimpleMessageC {
                Data = "Hello"
            });

            bool success = latch.Wait(5000);
            Assert.True(success, "Failed to receive all messages.");
        }

        [Fact(Skip = "Requires Azure Service Bus")]
        public void CanSubscribeToAllMessageTypes() {
            var latch = new CountDownLatch(3);
            _messageBus.Value.Subscribe<object>(msg => {
                latch.Signal();
            });
            _messageBus.Value.PublishAsync(new SimpleMessageA {
                Data = "Hello"
            });
            _messageBus.Value.PublishAsync(new SimpleMessageB {
                Data = "Hello"
            });
            _messageBus.Value.PublishAsync(new SimpleMessageC {
                Data = "Hello"
            });

            bool success = latch.Wait(5000);
            Assert.True(success, "Failed to receive all messages.");
        }
    }
}