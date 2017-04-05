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
        private long _totalSent;

        public InMemoryMailSender(int messagesToStore = 25) {
            _messagesToStore = messagesToStore;
        }

        public long TotalSent => _totalSent;
        public List<MailMessage> SentMessages => _recentMessages.ToList();
        public MailMessage LastMessage => SentMessages.LastOrDefault();

        public Task SendAsync(MailMessage model) {
            _recentMessages.Enqueue(model);
            Interlocked.Increment(ref _totalSent);

            while (_recentMessages.Count > _messagesToStore)
                _recentMessages.Dequeue();

            return Task.CompletedTask;
        }
    }
}
