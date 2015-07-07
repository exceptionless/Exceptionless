using System;
using System.Net;
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
            var message = model.ToMailMessage();
            message.Headers.Add("X-Mailer-Machine", Environment.MachineName);
            message.Headers.Add("X-Mailer-Date", DateTime.Now.ToString());

            var client = new SmtpClient();
            if (!String.IsNullOrEmpty(Settings.Current.SmtpHost)) {
                client.Host = Settings.Current.SmtpHost;
                client.Port = Settings.Current.SmtpPort;
                client.EnableSsl = Settings.Current.SmtpEnableSsl;
                client.Credentials = new NetworkCredential(Settings.Current.SmtpUser, Settings.Current.SmtpPassword);
            }

            await client.SendMailAsync(message);

            Interlocked.Increment(ref _messagesSent);
        }
    }
}
