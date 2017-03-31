using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using MailKit.Net.Smtp;
using MimeKit;
using MailMessage = Exceptionless.Core.Queues.Models.MailMessage;

namespace Exceptionless.Core.Mail {
    public class SmtpMailSender : IMailSender {
        private long _messagesSent;

        public long SentCount => _messagesSent;

        public async Task SendAsync(MailMessage model) {
            var message = model.ToMailMessage();

            message.Headers.Add("X-Mailer-Machine", Environment.MachineName);
            message.Headers.Add("X-Mailer-Date", SystemClock.UtcNow.ToString());
            message.Headers.Add("X-Auto-Response-Suppress", "All");
            message.Headers.Add("Auto-Submitted", "auto-generated");

            using (var client = new SmtpClient()) {
                // accept all SSL certificates (should this be changed?)
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                await client.ConnectAsync(Settings.Current.SmtpHost, Settings.Current.SmtpPort, Settings.Current.SmtpEnableSsl).AnyContext();

                // Note: since we don't have an OAuth2 token, disable the XOAUTH2 authentication mechanism.
                client.AuthenticationMechanisms.Remove("XOAUTH2");

                if ((Settings.Current.SmtpUser == null) != (Settings.Current.SmtpPassword == null))
                    throw new ArgumentException("Must specify both the SmtpUser and the SmtpPassword, or neither.");

                if (Settings.Current.SmtpUser != null)
                    await client.AuthenticateAsync(Settings.Current.SmtpUser, Settings.Current.SmtpPassword).AnyContext();

                await client.SendAsync(message).AnyContext();

                // we don't care if there is an error at this point.
                Interlocked.Increment(ref _messagesSent);
                await client.DisconnectAsync(true).AnyContext();
            }
        }
    }
}
