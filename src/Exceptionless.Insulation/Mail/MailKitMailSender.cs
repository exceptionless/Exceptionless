using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Foundatio.Utility;
using MailKit.Net.Smtp;
using MimeKit;
using MailMessage = Exceptionless.Core.Queues.Models.MailMessage;

namespace Exceptionless.Insulation.Mail {
    public class MailKitMailSender : IMailSender {
        private long _totalSent;

        public long TotalSent => _totalSent;

        public async Task SendAsync(MailMessage model) {
            var message = CreateMailMessage(model);
            message.Headers.Add("X-Mailer-Machine", Environment.MachineName);
            message.Headers.Add("X-Mailer-Date", SystemClock.UtcNow.ToString());
            message.Headers.Add("X-Auto-Response-Suppress", "All");
            message.Headers.Add("Auto-Submitted", "auto-generated");

            using (var client = new SmtpClient()) {
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                await client.ConnectAsync(Settings.Current.SmtpHost, Settings.Current.SmtpPort, Settings.Current.SmtpEnableSSL).AnyContext();

                // Note: since we don't have an OAuth2 token, disable the XOAUTH2 authentication mechanism.
                client.AuthenticationMechanisms.Remove("XOAUTH2");

                if (!String.IsNullOrEmpty(Settings.Current.SmtpUser))
                    await client.AuthenticateAsync(Settings.Current.SmtpUser, Settings.Current.SmtpPassword).AnyContext();

                await client.SendAsync(message).AnyContext();

                // we don't care if there is an error at this point.
                Interlocked.Increment(ref _totalSent);
                await client.DisconnectAsync(true).AnyContext();
            }
        }

        private MimeMessage CreateMailMessage(MailMessage notification) {
            var message = new MimeMessage { Subject = notification.Subject };
            var builder = new BodyBuilder();

            if (!String.IsNullOrEmpty(notification.To))
                message.To.AddRange(InternetAddressList.Parse(notification.To));

            if (!String.IsNullOrEmpty(notification.From))
                message.From.AddRange(InternetAddressList.Parse(notification.From));
            else
                message.From.AddRange(InternetAddressList.Parse(Settings.Current.SmtpFrom));

            if (!String.IsNullOrEmpty(notification.TextBody))
                builder.TextBody = notification.TextBody;

            if (!String.IsNullOrEmpty(notification.HtmlBody))
                builder.HtmlBody = notification.HtmlBody;

            message.Body = builder.ToMessageBody();
            return message;
        }
    }
}