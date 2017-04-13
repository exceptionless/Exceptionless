using System;
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

            var msg = _emailGenerator.GenerateMessage(new UserModel {
                User = user,
                BaseUrl = Settings.Current.BaseURL
            }, "PasswordReset").ToMailMessage();
            msg.To = user.EmailAddress;

            return QueueMessageAsync(msg, "passwordreset");
        }

        public Task SendVerifyEmailAsync(User user) {
            var msg = _emailGenerator.GenerateMessage(new UserModel {
                User = user,
                BaseUrl = Settings.Current.BaseURL
            }, "VerifyEmail").ToMailMessage();
            msg.To = user.EmailAddress;

            return QueueMessageAsync(msg, "verifyemail");
        }

        public Task SendInviteAsync(User sender, Organization organization, Invite invite) {
            var msg = _emailGenerator.GenerateMessage(new InviteModel {
                Sender = sender,
                Organization = organization,
                Invite = invite,
                BaseUrl = Settings.Current.BaseURL
            }, "Invite").ToMailMessage();
            msg.To = invite.EmailAddress;

            return QueueMessageAsync(msg, "invite");
        }

        public Task SendPaymentFailedAsync(User owner, Organization organization) {
            var msg = _emailGenerator.GenerateMessage(new PaymentModel {
                Owner = owner,
                Organization = organization,
                BaseUrl = Settings.Current.BaseURL
            }, "PaymentFailed").ToMailMessage();
            msg.To = owner.EmailAddress;

            return QueueMessageAsync(msg, "paymentfailed");
        }

        public Task SendAddedToOrganizationAsync(User sender, Organization organization, User user) {
            var msg = _emailGenerator.GenerateMessage(new AddedToOrganizationModel {
                Sender = sender,
                Organization = organization,
                User = user,
                BaseUrl = Settings.Current.BaseURL
            }, "AddedToOrganization").ToMailMessage();
            msg.To = user.EmailAddress;

            return QueueMessageAsync(msg, "addedtoorganization");
        }

        public Task SendEventNoticeAsync(string emailAddress, EventNotification model) {
            var msg = _pluginManager.GetEventNotificationMailMessage(model);
            if (msg == null) {
                _logger.Warn("Unable to create event notification mail message for event \"{0}\". User: \"{1}\"", model.EventId, emailAddress);
                return Task.CompletedTask;
            }

            msg.To = emailAddress;
            return QueueMessageAsync(msg, "eventnotice");
        }

        public Task SendOrganizationNoticeAsync(string emailAddress, OrganizationNotificationModel model) {
            model.BaseUrl = Settings.Current.BaseURL;

            var msg = _emailGenerator.GenerateMessage(model, "OrganizationNotice").ToMailMessage();
            msg.To = emailAddress;

            return QueueMessageAsync(msg, "organizationnotice");
        }

        public Task SendDailySummaryAsync(string emailAddress, DailySummaryModel notification) {
            notification.BaseUrl = Settings.Current.BaseURL;

            var msg = _emailGenerator.GenerateMessage(notification, "DailySummary").ToMailMessage();
            msg.To = emailAddress;

            return QueueMessageAsync(msg, "dailysummary");
        }

        private Task QueueMessageAsync(MailMessage message, string metricsName) {
            CleanAddresses(message);
            return Task.WhenAll(
                _metrics.CounterAsync($"mailer.{metricsName}"),
                _queue.EnqueueAsync(message)
            );
        }

        private static void CleanAddresses(MailMessage message) {
            if (Settings.Current.WebsiteMode == WebsiteMode.Production)
                return;

            if (Settings.Current.AllowedOutboundAddresses.Contains(message.To.ToLowerInvariant()))
                return;

            message.Subject = $"[{message.To}] {message.Subject}".StripInvisible();
            message.To = Settings.Current.TestEmailAddress;
        }
    }
}