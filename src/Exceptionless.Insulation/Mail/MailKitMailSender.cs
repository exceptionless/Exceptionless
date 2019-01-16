using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Foundatio.Utility;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MailMessage = Exceptionless.Core.Queues.Models.MailMessage;

namespace Exceptionless.Insulation.Mail {
    public class MailKitMailSender : IMailSender {
        private readonly IOptions<EmailOptions> _emailOptions;
        private readonly ILogger _logger;

        public MailKitMailSender(IOptions<EmailOptions> emailOptions, ILoggerFactory loggerFactory) {
            _emailOptions = emailOptions;
            _logger = loggerFactory.CreateLogger<MailKitMailSender>();
        }

        public async Task SendAsync(MailMessage model) {
            bool isTraceLogEnabled = _logger.IsEnabled(LogLevel.Trace);
            if (isTraceLogEnabled) _logger.LogTrace("Creating Mail Message from model");
            
            var message = CreateMailMessage(model);
            message.Headers.Add("X-Mailer-Machine", Environment.MachineName);
            message.Headers.Add("X-Mailer-Date", SystemClock.UtcNow.ToString());
            message.Headers.Add("X-Auto-Response-Suppress", "All");
            message.Headers.Add("Auto-Submitted", "auto-generated");

            using (var client = new SmtpClient()) {
                string host = _emailOptions.Value.SmtpHost;
                int port = _emailOptions.Value.SmtpPort;
                var encryption = GetSecureSocketOption(_emailOptions.Value.SmtpEncryption);
                if (isTraceLogEnabled) _logger.LogTrace("Connecting to SMTP server: {SmtpHost}:{SmtpPort} using {Encryption}", host, port, encryption);
                using (var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30))) {
                    await client.ConnectAsync(host, port, encryption, tokenSource.Token).AnyContext();
                }
                if (isTraceLogEnabled) _logger.LogTrace("Connected to SMTP server");

                // Note: since we don't have an OAuth2 token, disable the XOAUTH2 authentication mechanism.
                client.AuthenticationMechanisms.Remove("XOAUTH2");

                string user = _emailOptions.Value.SmtpUser;
                if (!String.IsNullOrEmpty(user)) {
                    if (isTraceLogEnabled) _logger.LogTrace("Authenticating {SmtpUser} to SMTP server", user);
                    await client.AuthenticateAsync(user, _emailOptions.Value.SmtpPassword).AnyContext();
                    if (isTraceLogEnabled) _logger.LogTrace("Authenticated to SMTP server", user);
                }

                if (isTraceLogEnabled) _logger.LogTrace("Sending message: to={To} subject={Subject}", message.Subject, message.To);
                await client.SendAsync(message).AnyContext();
                if (isTraceLogEnabled) _logger.LogTrace("Sent Message");
                await client.DisconnectAsync(true).AnyContext();
            }
            if (isTraceLogEnabled) _logger.LogTrace("Disconnected from SMTP server");
        }

        private SecureSocketOptions GetSecureSocketOption(SmtpEncryption encryption) {
            switch (encryption) {
                case SmtpEncryption.StartTLS:
                    return SecureSocketOptions.StartTls;
                case SmtpEncryption.SSL:
                    return SecureSocketOptions.SslOnConnect;
                default:
                    return SecureSocketOptions.Auto;
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
                message.From.AddRange(InternetAddressList.Parse(_emailOptions.Value.SmtpFrom));

            if (!String.IsNullOrEmpty(notification.Body))
                builder.HtmlBody = notification.Body;

            message.Body = builder.ToMessageBody();
            return message;
        }
    }
}