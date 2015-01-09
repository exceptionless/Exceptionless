using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Component;

namespace Exceptionless.Core.Messaging {
    public abstract class MessageBusBase : IMessagePublisher, IDisposable {
        private readonly CancellationTokenSource _queueDisposedCancellationTokenSource;
        private readonly List<DelayedMessage> _delayedMessages = new List<DelayedMessage>();

        public MessageBusBase() {
            _queueDisposedCancellationTokenSource = new CancellationTokenSource();
            TaskHelper.RunPeriodic(DoMaintenance, TimeSpan.FromMilliseconds(500), _queueDisposedCancellationTokenSource.Token, TimeSpan.FromMilliseconds(100));
        }

        private async Task DoMaintenance() {
            foreach (var message in _delayedMessages.Where(m => m.SendTime <= DateTime.Now).ToList()) {
                _delayedMessages.Remove(message);
                Publish(message.MessageType, message.Message);
            }
        }

        public virtual void Publish(Type messageType, object message, TimeSpan? delay = null) {
            if (message == null)
                throw new ArgumentNullException("message");

            if (delay.HasValue && delay.Value > TimeSpan.Zero)
                _delayedMessages.Add(new DelayedMessage { Message = message, MessageType = messageType, SendTime = DateTime.Now.Add(delay.Value) });
        }

        protected class DelayedMessage {
            public DateTime SendTime { get; set; }
            public Type MessageType { get; set; }
            public object Message { get; set; }
        }

        public void Dispose() {
            _queueDisposedCancellationTokenSource.Cancel();
        }
    }
}
