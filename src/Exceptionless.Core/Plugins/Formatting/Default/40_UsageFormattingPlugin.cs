using System;
using System.Collections.Generic;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(40)]
    public sealed class UsageFormattingPlugin : FormattingPluginBase {
        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsFeatureUsage();
        }

        public override SummaryData GetStackSummaryData(Stack stack) {
            if (!stack.SignatureInfo.ContainsKeyWithValue("Type", Event.KnownTypes.FeatureUsage))
                return null;

            return new SummaryData { TemplateKey = "stack-feature-summary", Data = new Dictionary<string, object>() };
        }

        public override string GetStackTitle(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return !String.IsNullOrEmpty(ev.Source) ? ev.Source : "(Unknown)";
        }

        public override SummaryData GetEventSummaryData(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            var data = new Dictionary<string, object> { { "Source", ev.Source } };
            AddUserIdentitySummaryData(data, ev.GetUserIdentity());

            return new SummaryData { TemplateKey = "event-feature-summary", Data = data };
        }

        public override MailMessageData GetEventNotificationMailMessageData(PersistentEvent ev, bool isCritical, bool isNew, bool isRegression) {
            if (!ShouldHandle(ev))
                return null;

            string subject = String.Concat("Feature: ", ev.Source).Truncate(120);
            var data = new Dictionary<string, object> {
                { "Source", ev.Source.Truncate(60) }
            };

            return new MailMessageData { Subject = subject, Data = data };
        }

        public override SlackMessage GetSlackEventNotification(PersistentEvent ev, Project project, bool isCritical, bool isNew, bool isRegression) {
            if (!ShouldHandle(ev))
                return null;

            var attachment = new SlackMessage.SlackAttachment(ev) {
                Fields = new List<SlackMessage.SlackAttachmentFields> {
                    new SlackMessage.SlackAttachmentFields {
                        Title = "Source",
                        Value = ev.Source.Truncate(60)
                    }
                }
            };

            AddDefaultSlackFields(ev, attachment.Fields, false);
            string subject = $"[{project.Name}] Feature: *{GetSlackEventUrl(ev.Id, ev.Source).Truncate(120)}*";
            return new SlackMessage(subject) {
                Attachments = new List<SlackMessage.SlackAttachment> { attachment }
            };
        }
    }
}