using System;
using System.Threading;
using Exceptionless.Core.Messaging;
using Xunit;

namespace Exceptionless.Api.Tests.Messaging {
    public class ServiceBusMessagingTests {
        private const string CONNECTION_STRING = "<ConnectionStringHere>";
        private static readonly Lazy<ServiceBusMessageBus> _messageBus = new Lazy<ServiceBusMessageBus>(() => new ServiceBusMessageBus(CONNECTION_STRING, "exceptionless-test"));

        [Fact]
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
            Assert.True(success, "Failed to recieve message.");
        }

        [Fact]
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
            Assert.True(success, "Failed to recieve all messages.");
        }

        [Fact]
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
            Assert.True(success, "Failed to recieve all messages.");
        }

        [Fact]
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
            Assert.True(success, "Failed to recieve message.");
        }

        [Fact]
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
            Assert.True(success, "Failed to recieve all messages.");
        }

        [Fact]
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
            Assert.True(success, "Failed to recieve all messages.");
        }
    }
}
