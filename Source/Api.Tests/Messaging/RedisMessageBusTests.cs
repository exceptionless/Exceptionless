using System;
using System.Threading;
using CodeSmith.Core.Threading;
using Exceptionless.Core;
using Exceptionless.Core.Messaging;
using StackExchange.Redis;
using Xunit;

namespace Exceptionless.Api.Tests.Messaging {
    public class RedisMessageBusTests {
        private readonly RedisMessageBus _messageBus;

        public RedisMessageBusTests() {
            //if (!Settings.Current.UseAzureServiceBus)
            //    return;

            var muxer = ConnectionMultiplexer.Connect(Settings.Current.RedisConnectionInfo.ToString());
            _messageBus = new RedisMessageBus(muxer.GetSubscriber());   
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
            _messageBus.Publish(new SimpleMessageA {
                Data = "Hello"
            });

            bool success = resetEvent.WaitOne(15000);
            Assert.True(success, "Failed to receive message.");
        }

        [Fact]
        public void WontKeepMessagesWithNoSubscribers() {
            if (_messageBus == null)
                return;

            _messageBus.Publish(new SimpleMessageA {
                Data = "Hello"
            });

            Thread.Sleep(1000);
            var resetEvent = new AutoResetEvent(false);
            _messageBus.Subscribe<SimpleMessageA>(msg => {
                Assert.Equal("Hello", msg.Data);
                resetEvent.Set();
            });

            bool success = resetEvent.WaitOne(2000);
            Assert.False(success, "Messages are building up.");
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
            _messageBus.Publish(new SimpleMessageA {
                Data = "Hello"
            });

            bool success = latch.Wait(15000);
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
            _messageBus.Publish(new SimpleMessageA {
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
            _messageBus.Publish(new SimpleMessageA {
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
            _messageBus.Publish(new SimpleMessageA {
                Data = "Hello"
            });
            _messageBus.Publish(new SimpleMessageB {
                Data = "Hello"
            });
            _messageBus.Publish(new SimpleMessageC {
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
            _messageBus.Publish(new SimpleMessageA {
                Data = "Hello"
            });
            _messageBus.Publish(new SimpleMessageB {
                Data = "Hello"
            });
            _messageBus.Publish(new SimpleMessageC {
                Data = "Hello"
            });

            bool success = latch.Wait(5000);
            Assert.True(success, "Failed to receive all messages.");
        }
    }
}