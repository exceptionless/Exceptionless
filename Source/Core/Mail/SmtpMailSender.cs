using System;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using MailMessage = Exceptionless.Core.Queues.Models.MailMessage;

namespace Exceptionless.Core.Mail {
    public class SmtpMailSender : IMailSender {
        private long _messagesSent = 0;

        public long SentCount { get { return _messagesSent; } }

        public async Task SendAsync(MailMessage model) {
            var client = new SmtpClient();
            var message = model.ToMailMessage();
            message.Headers.Add("X-Mailer-Machine", Environment.MachineName);
            message.Headers.Add("X-Mailer-Date", DateTime.Now.ToString());

            await client.SendAsync(message);

            Interlocked.Increment(ref _messagesSent);
        }
    }
}
