using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Models;
using Exceptionless.DateTimeExtensions;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Utility;
using HandlebarsDotNet;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Mail {
    public class Mailer : IMailer {
        private readonly ConcurrentDictionary<string, Func<object, string>> _cachedTemplates = new ConcurrentDictionary<string, Func<object, string>>();
        private readonly IQueue<MailMessage> _queue;
        private readonly FormattingPluginManager _pluginManager;
        private readonly IMetricsClient _metrics;
        private readonly ILogger _logger;

        public Mailer(IQueue<MailMessage> queue, FormattingPluginManager pluginManager, IMetricsClient metrics, ILogger<Mailer> logger) {
            _queue = queue;
            _pluginManager = pluginManager;
            _metrics = metrics;
            _logger = logger;
        }

        public async Task<bool> SendEventNoticeAsync(User user, PersistentEvent ev, Project project, bool isNew, bool isRegression, int totalOccurrences) {
            bool isCritical = ev.IsCritical();
            var result = _pluginManager.GetEventNotificationMailMessageData(ev, isCritical, isNew, isRegression);
            if (result == null || result.Data.Count == 0) {
                _logger.LogWarning("Unable to create event notification mail message for event \"{user}\". User: \"{EmailAddress}\"", ev.Id, user.EmailAddress);
                return false;
            }

            if (String.IsNullOrEmpty(result.Subject))
                result.Subject = ev.Message ?? ev.Source ?? "(Global)";

            var messageData = new Dictionary<string, object> {
                { "Subject", result.Subject },
                { "BaseUrl", Settings.Current.BaseURL },
                { "ProjectName", project.Name },
                { "ProjectId", project.Id },
                { "StackId", ev.StackId },
                { "EventId", ev.Id },
                { "IsCritical", isCritical },
                { "IsNew", isNew },
                { "IsRegression", isRegression },
                { "IsFixable", ev.Type == Event.KnownTypes.Error || ev.Type == Event.KnownTypes.NotFound },
                { "TotalOccurrences", totalOccurrences },
                { "Fields", result.Data }
            };

            AddDefaultFields(ev, result.Data);
            AddUserInfo(ev, messageData);

            const string template = "event-notice";
            await QueueMessageAsync(new MailMessage {
                To = user.EmailAddress,
                Subject = $"[{project.Name}] {result.Subject}",
                Body = RenderTemplate(template, messageData)
            }, template).AnyContext();
            return true;
        }

        private void AddUserInfo(PersistentEvent ev, Dictionary<string, object> data) {
            var ud = ev.GetUserDescription();
            var ui = ev.GetUserIdentity();
            if (!String.IsNullOrEmpty(ud?.Description))
                data["UserDescription"] = ud.Description;

            if (!String.IsNullOrEmpty(ud?.EmailAddress))
                data["UserEmail"] = ud.EmailAddress;

            string displayName = null;
            if (!String.IsNullOrEmpty(ui?.Identity))
                data["UserIdentity"] = displayName = ui.Identity;

            if (!String.IsNullOrEmpty(ui?.Name))
                data["UserName"] = displayName = ui.Name;

            if (!String.IsNullOrEmpty(displayName) && !String.IsNullOrEmpty(ud?.EmailAddress))
                displayName = $"{displayName} ({ud.EmailAddress})";
            else if (!String.IsNullOrEmpty(ui?.Identity) && !String.IsNullOrEmpty(ui.Name))
                displayName = $"{ui.Name} ({ui.Identity})";

            if (!String.IsNullOrEmpty(displayName))
                data["UserDisplayName"] = displayName;

            data["HasUserInfo"] = ud != null || ui != null;
        }

        private void AddDefaultFields(PersistentEvent ev, Dictionary<string, object> data) {
            if (ev.Tags.Count > 0)
                data["Tags"] = String.Join(", ", ev.Tags);

            if (ev.Value.GetValueOrDefault() != 0)
                data["Value"] = ev.Value;

            string version = ev.GetVersion();
            if (!String.IsNullOrEmpty(version))
                data["Version"] = version;
        }

        public Task SendOrganizationAddedAsync(User sender, Organization organization, User user) {
            const string template = "organization-added";
            string subject = $"{sender.FullName} added you to the organization \"{organization.Name}\" on Exceptionless";
            var data = new Dictionary<string, object> {
                { "Subject", subject },
                { "BaseUrl", Settings.Current.BaseURL },
                { "OrganizationId", organization.Id },
                { "OrganizationName", organization.Name }
            };

            return QueueMessageAsync(new MailMessage {
                To = user.EmailAddress,
                Subject = subject,
                Body = RenderTemplate(template, data)
            }, template);
        }

        public Task SendOrganizationInviteAsync(User sender, Organization organization, Invite invite) {
            const string template = "organization-invited";
            string subject = $"{sender.FullName} invited you to join the organization \"{organization.Name}\" on Exceptionless";
            var data = new Dictionary<string, object> {
                { "Subject", subject },
                { "BaseUrl", Settings.Current.BaseURL },
                { "InviteToken", invite.Token }
            };

            return QueueMessageAsync(new MailMessage {
                To = invite.EmailAddress,
                Subject = subject,
                Body = RenderTemplate(template, data)
            }, template);
        }

        public Task SendOrganizationNoticeAsync(User user, Organization organization, bool isOverMonthlyLimit, bool isOverHourlyLimit) {
            const string template = "organization-notice";
            string subject = isOverMonthlyLimit
                    ? $"[{organization.Name}] Monthly plan limit exceeded."
                    : $"[{organization.Name}] Events are currently being throttled.";

            var data = new Dictionary<string, object> {
                { "Subject", subject },
                { "BaseUrl", Settings.Current.BaseURL },
                { "OrganizationId", organization.Id },
                { "OrganizationName", organization.Name },
                { "IsOverMonthlyLimit", isOverMonthlyLimit },
                { "IsOverHourlyLimit", isOverHourlyLimit },
                { "ThrottledUntil", SystemClock.UtcNow.StartOfHour().AddHours(1).ToShortTimeString() }
            };

            return QueueMessageAsync(new MailMessage {
                To = user.EmailAddress,
                Subject = subject,
                Body = RenderTemplate(template, data)
            }, template);
        }

        public Task SendOrganizationPaymentFailedAsync(User owner, Organization organization) {
            const string template = "organization-payment-failed";
            string subject = $"[{organization.Name}] Payment failed! Update billing information to avoid service interruption!";
            var data = new Dictionary<string, object> {
                { "Subject", subject },
                { "BaseUrl", Settings.Current.BaseURL },
                { "OrganizationId", organization.Id },
                { "OrganizationName", organization.Name }
            };

            return QueueMessageAsync(new MailMessage {
                To = owner.EmailAddress,
                Subject = subject,
                Body = RenderTemplate(template, data)
            }, template);
        }

        public Task SendProjectDailySummaryAsync(User user, Project project, IEnumerable<Stack> mostFrequent, IEnumerable<Stack> newest, DateTime startDate, bool hasSubmittedEvents, double count, double uniqueCount, double newCount, double fixedCount, int blockedCount, int tooBigCount, bool isFreePlan) {
            const string template = "project-daily-summary";
            string subject = $"[{project.Name}] Summary for {startDate.ToLongDateString()}";
            var data = new Dictionary<string, object> {
                { "Subject", subject },
                { "BaseUrl", Settings.Current.BaseURL },
                { "OrganizationId", project.OrganizationId },
                { "ProjectId", project.Id },
                { "ProjectName", project.Name },
                { "MostFrequent", GetStackTemplateData(mostFrequent) },
                { "Newest", GetStackTemplateData(newest) },
                { "StartDate", startDate.ToLongDateString() },
                { "HasSubmittedEvents", hasSubmittedEvents },
                { "Count", count },
                { "Unique", uniqueCount },
                { "New", newCount },
                { "Fixed", fixedCount },
                { "Blocked", blockedCount },
                { "TooBig", tooBigCount },
                { "IsFreePlan", isFreePlan }
            };

            return QueueMessageAsync(new MailMessage {
                To = user.EmailAddress,
                Subject = subject,
                Body = RenderTemplate(template, data)
            }, template);
        }

        private static IEnumerable<object> GetStackTemplateData(IEnumerable<Stack> stacks) {
            return stacks?.Select(s => new {
                StackId = s.Id,
                Title = s.Title.Truncate(50),
                TypeName = s.GetTypeName().Truncate(50),
                s.IsRegressed,
            });
        }

        public Task SendUserEmailVerifyAsync(User user) {
            if (String.IsNullOrEmpty(user?.VerifyEmailAddressToken))
                return Task.CompletedTask;

            const string template = "user-email-verify";
            const string subject = "Exceptionless Account Confirmation";
            var data = new Dictionary<string, object> {
                { "Subject", subject },
                { "BaseUrl", Settings.Current.BaseURL },
                { "UserFullName", user.FullName },
                { "UserVerifyEmailAddressToken", user.VerifyEmailAddressToken }
            };

            return QueueMessageAsync(new MailMessage {
                To = user.EmailAddress,
                Subject = subject,
                Body = RenderTemplate(template, data)
            }, template);
        }

        public Task SendUserPasswordResetAsync(User user) {
            if (String.IsNullOrEmpty(user?.PasswordResetToken))
                return Task.CompletedTask;

            const string template = "user-password-reset";
            const string subject = "Exceptionless Password Reset";
            var data = new Dictionary<string, object> {
                { "Subject", subject },
                { "BaseUrl", Settings.Current.BaseURL },
                { "UserFullName", user.FullName },
                { "UserPasswordResetToken", user.PasswordResetToken }
            };

            return QueueMessageAsync(new MailMessage {
                To = user.EmailAddress,
                Subject = subject,
                Body = RenderTemplate(template, data)
            }, template);
        }

        private string RenderTemplate(string name, IDictionary<string, object> data) {
            var template = GetCompiledTemplate(name);
            return template(data);
        }

        private Func<object, string> GetCompiledTemplate(string name) {
            return _cachedTemplates.GetOrAdd(name, templateName => {
                var assembly = typeof(Mailer).Assembly;
                string resourceName = $"Exceptionless.Core.Mail.Templates.{templateName}.html";

                using (var stream = assembly.GetManifestResourceStream(resourceName)) {
                    using (var reader = new StreamReader(stream)) {
                        string template = reader.ReadToEnd();
                        return Handlebars.Compile(template);
                    }
                }
            });
        }

        private Task QueueMessageAsync(MailMessage message, string metricsName) {
            CleanAddresses(message);
            _metrics.Counter($"mailer.{metricsName}");
            return _queue.EnqueueAsync(message);
        }

        private static void CleanAddresses(MailMessage message) {
            if (Settings.Current.AppMode == AppMode.Production)
                return;

            string address = message.To.ToLowerInvariant();
            if (Settings.Current.AllowedOutboundAddresses.Any(address.Contains))
                return;

            message.Subject = $"[{message.To}] {message.Subject}".StripInvisible();
            message.To = Settings.Current.TestEmailAddress;
        }
    }
}