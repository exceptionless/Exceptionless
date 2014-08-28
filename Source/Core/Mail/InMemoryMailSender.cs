using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Queues.Models;

namespace Exceptionless.Core.Mail {
    public class InMemoryMailSender : IMailSender {
        private readonly Queue<MailMessage> _recentMessages = new Queue<MailMessage>();
        private readonly int _messagesToStore;
        private long _totalSent = 0;
        private EventWaitHandle _waitHandle = new AutoResetEvent(false);

        public InMemoryMailSender(int messagesToStore = 25) {
            _messagesToStore = messagesToStore;
        }

        public void WaitForSend(long count = 1, double timeoutInSeconds = 10, Action work = null) {
            if (count == 0)
                return;

            long currentCount = _totalSent;
            if (work != null)
                work();

            count = count - (_totalSent - currentCount);

            do {
                if (!_waitHandle.WaitOne(TimeSpan.FromSeconds(timeoutInSeconds)))
                    throw new TimeoutException();

                count--;
            } while (count > 0);
        }

        public long TotalSent { get { return _totalSent; } }
        public List<MailMessage> SentMessages { get { return _recentMessages.ToList(); } }
        public MailMessage LastMessage { get { return SentMessages.Last(); } }

        public Task SendAsync(MailMessage model) {
            _recentMessages.Enqueue(model);
            Interlocked.Increment(ref _totalSent);
            _waitHandle.Set();

            while (_recentMessages.Count > _messagesToStore)
                _recentMessages.Dequeue();

            return Task.FromResult(0);
        }
    }
}
