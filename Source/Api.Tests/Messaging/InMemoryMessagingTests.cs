using System;
using System.Threading;
using CodeSmith.Core.Threading;
using Exceptionless.Core.Messaging;
using Xunit;

namespace Exceptionless.Api.Tests.Messaging {
    public class InMemoryMessagingTests {
        [Fact]
        public void CanSendMessage() {
            var resetEvent = new AutoResetEvent(false);
            var messageBus = new InMemoryMessageBus();
            messageBus.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                resetEvent.Set();
            });
            messageBus.Publish(new SimpleMessageA {
                Data = "Hello"
            });

            bool success = resetEvent.WaitOne(100);
            Assert.True(success, "Failed to receive message.");
        }

        [Fact]
        public void CanSendMessageToMultipleSubscribers() {
            var latch = new CountDownLatch(3);
            var messageBus = new InMemoryMessageBus();
            messageBus.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            messageBus.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            messageBus.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            messageBus.Publish(new SimpleMessageA {
                Data = "Hello"
            });

            bool success = latch.Wait(100);
            Assert.True(success, "Failed to receive all messages.");
        }

        [Fact]
        public void CanTolerateSubscriberFailure() {
            var latch = new CountDownLatch(2);
            var messageBus = new InMemoryMessageBus();
            messageBus.Subscribe<SimpleMessageA>(msg => {
                throw new ApplicationException();
            });
            messageBus.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            messageBus.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            messageBus.Publish(new SimpleMessageA {
                Data = "Hello"
            });

            bool success = latch.Wait(900);
            Assert.True(success, "Failed to receive all messages.");
        }

        [Fact]
        public void WillOnlyReceiveSubscribedMessageType() {
            var resetEvent = new AutoResetEvent(false);
            var messageBus = new InMemoryMessageBus();
            messageBus.Subscribe<SimpleMessageB>(msg => {
                Assert.True(false, "Received wrong message type.");
            });
            messageBus.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                resetEvent.Set();
            });
            messageBus.Publish(new SimpleMessageA {
                Data = "Hello"
            });

            bool success = resetEvent.WaitOne(100);
            Assert.True(success, "Failed to receive message.");
        }

        [Fact]
        public void WillReceiveDerivedMessageTypes() {
            var latch = new CountDownLatch(2);
            var messageBus = new InMemoryMessageBus();
            messageBus.Subscribe<ISimpleMessage>(msg => {
                Assert.Equal("Hello", msg.Data);
                latch.Signal();
            });
            messageBus.Publish(new SimpleMessageA {
                Data = "Hello"
            });
            messageBus.Publish(new SimpleMessageB {
                Data = "Hello"
            });
            messageBus.Publish(new SimpleMessageC {
                Data = "Hello"
            });

            bool success = latch.Wait(100);
            Assert.True(success, "Failed to receive all messages.");
        }

        [Fact]
        public void CanSubscribeToAllMessageTypes() {
            var latch = new CountDownLatch(3);
            var messageBus = new InMemoryMessageBus();
            messageBus.Subscribe<object>(msg => {
                latch.Signal();
            });
            messageBus.Publish(new SimpleMessageA {
                Data = "Hello"
            });
            messageBus.Publish(new SimpleMessageB {
                Data = "Hello"
            });
            messageBus.Publish(new SimpleMessageC {
                Data = "Hello"
            });

            bool success = latch.Wait(100);
            Assert.True(success, "Failed to receive all messages.");
        }
    }
}
