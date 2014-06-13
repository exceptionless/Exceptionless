using System;
using System.Threading;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Messaging;
using Xunit;

namespace Exceptionless.Api.Tests.Messaging {
    public class ServiceBusMessagingTests {
        private readonly ServiceBusMessageBus _messageBus;

        public ServiceBusMessagingTests() {
            if (!Settings.Current.UseAzureServiceBus)
                return;

            _messageBus = new ServiceBusMessageBus(Settings.Current.AzureServiceBusConnectionString, "test-messagebus");   
        }

        [Fact]
        public void CanSendMessage() {
            if (_messageBus == null)
                return;

            var resetEvent = new AutoResetEvent(false);
            _messageBus.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                resetEvent.Set();
            });
            _messageBus.PublishAsync(new SimpleMessageA {
                Data = "Hello"
            });

            bool success = resetEvent.WaitOne(5000);
            Assert.True(success, "Failed to receive message.");
        }

        [Fact]
        public void CanSendMessageToMultipleSubscribers() {
            if (_messageBus == null)
                return;

            var latch = new CountDownLatch(3);
            _messageBus.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            _messageBus.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            _messageBus.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            _messageBus.PublishAsync(new SimpleMessageA {
                Data = "Hello"
            });

            bool success = latch.Wait(5000);
            Assert.True(success, "Failed to receive all messages.");
        }

        [Fact]
        public void CanTolerateSubscriberFailure() {
            if (_messageBus == null)
                return;

            var latch = new CountDownLatch(2);
            _messageBus.Subscribe<SimpleMessageA>(msg => {
                throw new ApplicationException();
            });
            _messageBus.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            _messageBus.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            _messageBus.PublishAsync(new SimpleMessageA {
                Data = "Hello"
            });

            bool success = latch.Wait(5000);
            Assert.True(success, "Failed to receive all messages.");
        }

        [Fact]
        public void WillOnlyReceiveSubscribedMessageType() {
            if (_messageBus == null)
                return;

            var resetEvent = new AutoResetEvent(false);
            _messageBus.Subscribe<SimpleMessageB>(msg => {
                Assert.True(false, "Received wrong message type.");
            });
            _messageBus.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                resetEvent.Set();
            });
            _messageBus.PublishAsync(new SimpleMessageA {
                Data = "Hello"
            });

            bool success = resetEvent.WaitOne(5000);
            Assert.True(success, "Failed to receive message.");
        }

        [Fact]
        public void WillReceiveDerivedMessageTypes() {
            if (_messageBus == null)
                return;

            var latch = new CountDownLatch(2);
            _messageBus.Subscribe<ISimpleMessage>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            _messageBus.PublishAsync(new SimpleMessageA {
                Data = "Hello"
            });
            _messageBus.PublishAsync(new SimpleMessageB {
                Data = "Hello"
            });
            _messageBus.PublishAsync(new SimpleMessageC {
                Data = "Hello"
            });

            bool success = latch.Wait(5000);
            Assert.True(success, "Failed to receive all messages.");
        }

        [Fact]
        public void CanSubscribeToAllMessageTypes() {
            if (_messageBus == null)
                return;

            var latch = new CountDownLatch(3);
            _messageBus.Subscribe<object>(msg => {
                latch.Signal();
            });
            _messageBus.PublishAsync(new SimpleMessageA {
                Data = "Hello"
            });
            _messageBus.PublishAsync(new SimpleMessageB {
                Data = "Hello"
            });
            _messageBus.PublishAsync(new SimpleMessageC {
                Data = "Hello"
            });

            bool success = latch.Wait(5000);
            Assert.True(success, "Failed to receive all messages.");
        }
    }
}