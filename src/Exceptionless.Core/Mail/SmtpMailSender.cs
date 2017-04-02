using System;
using System.Configuration;
using System.Net.Configuration;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues.Models;
using Foundatio.Utility;

namespace Exceptionless.Core.Mail {
    public class SmtpMailSender : IMailSender {
        private readonly string _host;
        private readonly int _port;
        private readonly bool _enableSsl;
        private readonly string _username;
        private readonly string _password;
        private long _messagesSent;

        public SmtpMailSender() {
            var section = (SmtpSection)ConfigurationManager.GetSection("system.net/mailSettings/smtp");
            if (section != null && section.Network != null) {
                _host = section.Network.Host;
                _port = section.Network.Port;
                _enableSsl = section.Network.EnableSsl;
                _username = section.Network.UserName;
                _password = section.Network.Password;
            }
            if (!string.IsNullOrEmpty(Settings.Current.SmtpHost)) {
                _host = Settings.Current.SmtpHost;
                _enableSsl = Settings.Current.SmtpEnableSsl;
                _username = Settings.Current.SmtpUser;
                _password = Settings.Current.SmtpPassword;
                _port = Settings.Current.SmtpPort;
            }
            if (_port == 0) {
                _port = 25;
            }
        }

        public long SentCount => _messagesSent;

        public async Task SendAsync(MailMessage model) {
            var message = model.ToMimeMessage();
            message.Headers.Add("X-Mailer-Machine", Environment.MachineName);
            message.Headers.Add("X-Mailer-Date", SystemClock.UtcNow.ToString());
            message.Headers.Add("X-Auto-Response-Suppress", "All");
            message.Headers.Add("Auto-Submitted", "auto-generated");

            using (var client = new MailKit.Net.Smtp.SmtpClient()) {
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                await client.ConnectAsync(_host, _port, _enableSsl).AnyContext();
                // Note: since we don't have an OAuth2 token, disable
                // the XOAUTH2 authentication mechanism.
                client.AuthenticationMechanisms.Remove("XOAUTH2");
                if (!string.IsNullOrEmpty(_username)) {
                    await client.AuthenticateAsync(_username, _password).AnyContext();
                }
                await client.SendAsync(message).AnyContext();
                await client.DisconnectAsync(true).AnyContext();
            }
            Interlocked.Increment(ref _messagesSent);
        }
    }
}
