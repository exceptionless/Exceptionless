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

        public Task SendPasswordResetAsync(User user) {
            if (String.IsNullOrEmpty(user?.PasswordResetToken))
                return Task.CompletedTask;

            System.Net.Mail.MailMessage msg = _emailGenerator.GenerateMessage(new UserModel {
                User = user,
                BaseUrl = Settings.Current.BaseURL
            }, "PasswordReset");
            msg.To.Add(user.EmailAddress);

            return QueueMessageAsync(msg, "passwordreset");
        }

        public Task SendVerifyEmailAsync(User user) {
            System.Net.Mail.MailMessage msg = _emailGenerator.GenerateMessage(new UserModel {
                User = user,
                BaseUrl = Settings.Current.BaseURL
            }, "VerifyEmail");
            msg.To.Add(user.EmailAddress);

            return QueueMessageAsync(msg, "verifyemail");
        }

        public Task SendInviteAsync(User sender, Organization organization, Invite invite) {
            System.Net.Mail.MailMessage msg = _emailGenerator.GenerateMessage(new InviteModel {
                Sender = sender,
                Organization = organization,
                Invite = invite,
                BaseUrl = Settings.Current.BaseURL
            }, "Invite");
            msg.To.Add(invite.EmailAddress);

            return QueueMessageAsync(msg, "invite");
        }

        public Task SendPaymentFailedAsync(User owner, Organization organization) {
            System.Net.Mail.MailMessage msg = _emailGenerator.GenerateMessage(new PaymentModel {
                Owner = owner,
                Organization = organization,
                BaseUrl = Settings.Current.BaseURL
            }, "PaymentFailed");
            msg.To.Add(owner.EmailAddress);

            return QueueMessageAsync(msg, "paymentfailed");
        }

        public Task SendAddedToOrganizationAsync(User sender, Organization organization, User user) {
            System.Net.Mail.MailMessage msg = _emailGenerator.GenerateMessage(new AddedToOrganizationModel {
                Sender = sender,
                Organization = organization,
                User = user,
                BaseUrl = Settings.Current.BaseURL
            }, "AddedToOrganization");
            msg.To.Add(user.EmailAddress);

            return QueueMessageAsync(msg, "addedtoorganization");
        }

        public Task SendEventNoticeAsync(string emailAddress, EventNotification model) {
            var msg = _pluginManager.GetEventNotificationMailMessage(model);
            if (msg == null) {
                _logger.Warn("Unable to create event notification mail message for event \"{0}\". User: \"{1}\"", model.EventId, emailAddress);
                return Task.CompletedTask;
            }

            msg.To = emailAddress;
            return QueueMessageAsync(msg.ToSystemNetMailMessage(), "eventnotice");
        }

        public Task SendOrganizationNoticeAsync(string emailAddress, OrganizationNotificationModel model) {
            model.BaseUrl = Settings.Current.BaseURL;

            System.Net.Mail.MailMessage msg = _emailGenerator.GenerateMessage(model, "OrganizationNotice");
            msg.To.Add(emailAddress);

            return QueueMessageAsync(msg, "organizationnotice");
        }

        public Task SendDailySummaryAsync(string emailAddress, DailySummaryModel notification) {
            notification.BaseUrl = Settings.Current.BaseURL;
            System.Net.Mail.MailMessage msg = _emailGenerator.GenerateMessage(notification, "DailySummary");
            msg.To.Add(emailAddress);

            return QueueMessageAsync(msg, "dailysummary");
        }

        private async Task QueueMessageAsync(System.Net.Mail.MailMessage message, string metricsName) {
            await _metrics.CounterAsync($"mailer.{metricsName}").AnyContext();

            CleanAddresses(message);
            await _queue.EnqueueAsync(message.ToMailMessage()).AnyContext();
        }

        private static void CleanAddresses(System.Net.Mail.MailMessage message) {
            if (Settings.Current.WebsiteMode == WebsiteMode.Production)
                return;

            var invalid = new List<string>();
            invalid.AddRange(CleanAddresses(message.To));
            invalid.AddRange(CleanAddresses(message.CC));
            invalid.AddRange(CleanAddresses(message.Bcc));

            if (invalid.Count == 0)
                return;

            if (invalid.Count <= 3)
                message.Subject = String.Concat("[", invalid.ToDelimitedString(), "] ", message.Subject).StripInvisible();

            message.To.Add(Settings.Current.TestEmailAddress);
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