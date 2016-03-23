using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Models;
using Foundatio.Logging;
using Foundatio.Metrics;
using Foundatio.Queues;
using RazorSharpEmail;
using MailMessage = Exceptionless.Core.Queues.Models.MailMessage;

namespace Exceptionless.Core.Mail {
    public class Mailer : IMailer {
        private readonly IEmailGenerator _emailGenerator;
        private readonly IQueue<MailMessage> _queue;
        private readonly FormattingPluginManager _pluginManager;
        private readonly IMetricsClient _metrics;
        private readonly ILogger _logger;

        public Mailer(IEmailGenerator emailGenerator, IQueue<MailMessage> queue, FormattingPluginManager pluginManager, IMetricsClient metrics, ILogger<Mailer> logger) {
            _emailGenerator = emailGenerator;
            _queue = queue;
            _pluginManager = pluginManager;
            _metrics = metrics;
            _logger = logger;
        }

        public async Task SendPasswordResetAsync(User user) {
            if (String.IsNullOrEmpty(user?.PasswordResetToken))
                return;

            System.Net.Mail.MailMessage msg = _emailGenerator.GenerateMessage(new UserModel {
                User = user,
                BaseUrl = Settings.Current.BaseURL
            }, "PasswordReset");
            msg.To.Add(user.EmailAddress);

            await _metrics.CounterAsync("mailer.passwordreset").AnyContext();
            await QueueMessageAsync(msg).AnyContext();
        }

        public async Task SendVerifyEmailAsync(User user) {
            System.Net.Mail.MailMessage msg = _emailGenerator.GenerateMessage(new UserModel {
                User = user,
                BaseUrl = Settings.Current.BaseURL
            }, "VerifyEmail");
            msg.To.Add(user.EmailAddress);

            await _metrics.CounterAsync("mailer.verifyemail").AnyContext();
            await QueueMessageAsync(msg).AnyContext();
        }

        public async Task SendInviteAsync(User sender, Organization organization, Invite invite) {
            System.Net.Mail.MailMessage msg = _emailGenerator.GenerateMessage(new InviteModel {
                Sender = sender,
                Organization = organization,
                Invite = invite,
                BaseUrl = Settings.Current.BaseURL
            }, "Invite");
            msg.To.Add(invite.EmailAddress);

            await _metrics.CounterAsync("mailer.invite").AnyContext();
            await QueueMessageAsync(msg).AnyContext();
        }

        public async Task SendPaymentFailedAsync(User owner, Organization organization) {
            System.Net.Mail.MailMessage msg = _emailGenerator.GenerateMessage(new PaymentModel {
                Owner = owner,
                Organization = organization,
                BaseUrl = Settings.Current.BaseURL
            }, "PaymentFailed");
            msg.To.Add(owner.EmailAddress);

            await _metrics.CounterAsync("mailer.paymentfailed").AnyContext();
            await QueueMessageAsync(msg).AnyContext();
        }

        public async Task SendAddedToOrganizationAsync(User sender, Organization organization, User user) {
            System.Net.Mail.MailMessage msg = _emailGenerator.GenerateMessage(new AddedToOrganizationModel {
                Sender = sender,
                Organization = organization,
                User = user,
                BaseUrl = Settings.Current.BaseURL
            }, "AddedToOrganization");
            msg.To.Add(user.EmailAddress);

            await _metrics.CounterAsync("mailer.addedtoorganization").AnyContext();
            await QueueMessageAsync(msg).AnyContext();
        }

        public async Task SendNoticeAsync(string emailAddress, EventNotification model) {
            var message = _pluginManager.GetEventNotificationMailMessage(model);
            if (message == null) {
                _logger.Warn().Message("Unable to create event notification mail message for event \"{0}\". User: \"{1}\"", model.EventId, emailAddress).Write();
                return;
            }

            message.To = emailAddress;

            await _metrics.CounterAsync("mailer.eventnotification").AnyContext();
            await QueueMessageAsync(message.ToMailMessage()).AnyContext();
        }

        public async Task SendDailySummaryAsync(string emailAddress, DailySummaryModel notification) {
            notification.BaseUrl = Settings.Current.BaseURL;
            System.Net.Mail.MailMessage msg = _emailGenerator.GenerateMessage(notification, "DailySummary");
            msg.To.Add(emailAddress);

            await _metrics.CounterAsync("mailer.dailysummary").AnyContext();
            await QueueMessageAsync(msg).AnyContext();
        }

        private Task QueueMessageAsync(System.Net.Mail.MailMessage message) {
            CleanAddresses(message);
            return _queue.EnqueueAsync(message.ToMailMessage());
        }

        private static void CleanAddresses(System.Net.Mail.MailMessage msg) {
            if (Settings.Current.WebsiteMode == WebsiteMode.Production)
                return;

            var invalid = new List<string>();
            invalid.AddRange(CleanAddresses(msg.To));
            invalid.AddRange(CleanAddresses(msg.CC));
            invalid.AddRange(CleanAddresses(msg.Bcc));

            if (invalid.Count == 0)
                return;

            if (invalid.Count <= 3)
                msg.Subject = String.Concat("[", invalid.ToDelimitedString(), "] ", msg.Subject).StripInvisible();

            msg.To.Add(Settings.Current.TestEmailAddress);
        }

        private static IEnumerable<string> CleanAddresses(MailAddressCollection mac) {
            var invalid = new List<string>();
            for (int i = 0; i < mac.Count; i++) {
                if (!Settings.Current.AllowedOutboundAddresses.Contains(v => mac[i].Address.ToLowerInvariant().Contains(v))) {
                    invalid.Add(mac[i].Address);
                    mac.RemoveAt(i);
                    i--;
                }
            }

            return invalid;
        }
    }
}