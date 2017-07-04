using System;
using System.Collections.Generic;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(30)]
    public sealed class NotFoundFormattingPlugin : FormattingPluginBase {
        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsNotFound();
        }

        public override SummaryData GetStackSummaryData(Stack stack) {
            if (!stack.SignatureInfo.ContainsKeyWithValue("Type", Event.KnownTypes.NotFound))
                return null;

            return new SummaryData { TemplateKey = "stack-notfound-summary", Data = new Dictionary<string, object>() };
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

            return new SummaryData { TemplateKey = "event-notfound-summary", Data = data };
        }

        public override MailMessageData GetEventNotificationMailMessageData(PersistentEvent ev, bool isCritical, bool isNew, bool isRegression) {
            if (!ShouldHandle(ev))
                return null;

            string notificationType = "Occurrence 404";
            if (isNew)
                notificationType = "New 404";
            else if (isRegression)
                notificationType = "Regression 404";

            if (isCritical)
                notificationType = String.Concat("Critical ", notificationType.ToLowerInvariant());

            string subject = String.Concat(notificationType, ": ", ev.Source).Truncate(120);
            var requestInfo = ev.GetRequestInfo();
            var data = new Dictionary<string, object> {
                { "Url", requestInfo?.GetFullPath(true, true, true) ?? ev.Source.Truncate(60) }
            };

            return new MailMessageData { Subject = subject, Data = data };
        }

        public override SlackMessage GetSlackEventNotification(PersistentEvent ev, Project project, bool isCritical, bool isNew, bool isRegression) {
            if (!ShouldHandle(ev))
                return null;

            string notificationType = "occurrence 404";
            if (isNew)
                notificationType = "new 404";
            else if (isRegression)
                notificationType = "regression 404";

            if (isCritical)
                notificationType = String.Concat("critical ", notificationType);

            var requestInfo = ev.GetRequestInfo();
            var attachment = new SlackMessage.SlackAttachment(ev) {
                Color = "#BB423F",
                Fields = new List<SlackMessage.SlackAttachmentFields> {
                    new SlackMessage.SlackAttachmentFields {
                        Title = "Url",
                        Value = requestInfo != null ? requestInfo.GetFullPath(true, true, true) : ev.Source.Truncate(60)
                    }
                }
            };

            AddDefaultSlackFields(ev, attachment.Fields, false);
            string subject = $"[{project.Name}] A {notificationType}: *{GetSlackEventUrl(ev.Id, ev.Source.Truncate(120))}*";
            return new SlackMessage(subject) {
                Attachments = new List<SlackMessage.SlackAttachment> { attachment }
            };
        }
    }
}