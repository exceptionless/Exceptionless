using System;
using System.Collections.Generic;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(99)]
    public sealed class DefaultFormattingPlugin : FormattingPluginBase {
        public override string GetStackTitle(PersistentEvent ev) {
            if (String.IsNullOrWhiteSpace(ev.Message) && ev.IsError())
                return "Unknown Error";

            return ev.Message ?? ev.Source ?? $"{ev.Type} Event".TrimStart();
        }

        public override SummaryData GetStackSummaryData(Stack stack) {
            var data = new Dictionary<string, object> { { "Type", stack.Type } };

            if (stack.SignatureInfo.TryGetValue("Source", out string value))
                data.Add("Source", value);

            return new SummaryData { TemplateKey = "stack-summary", Data = data };
        }

        public override SummaryData GetEventSummaryData(PersistentEvent ev) {
            var data = new Dictionary<string, object> {
                { "Message", GetStackTitle(ev) },
                { "Source", ev.Source },
                { "Type", ev.Type }
            };

            AddUserIdentitySummaryData(data, ev.GetUserIdentity());

            return new SummaryData { TemplateKey = "event-summary", Data = data };
        }

        public override MailMessageData GetEventNotificationMailMessageData(PersistentEvent ev, bool isCritical, bool isNew, bool isRegression) {
            string messageOrSource = !String.IsNullOrEmpty(ev.Message) ? ev.Message : ev.Source;
            if (String.IsNullOrEmpty(messageOrSource))
                return null;

            string notificationType = "Occurrence event";
            if (isNew)
                notificationType = "New event";
            else if (isRegression)
                notificationType = "Regression event";

            if (isCritical)
                notificationType = String.Concat("Critical ", notificationType.ToLowerInvariant());

            string subject = String.Concat(notificationType, ": ", messageOrSource).Truncate(120);
            var data = new Dictionary<string, object>();
            if (!String.IsNullOrEmpty(ev.Message))
                data.Add("Message", ev.Message.Truncate(60));

            if (!String.IsNullOrEmpty(ev.Source))
                data.Add("Source", ev.Source.Truncate(60));

            var requestInfo = ev.GetRequestInfo();
            if (requestInfo != null)
                data.Add("Url", requestInfo.GetFullPath(true, true, true));

            return new MailMessageData { Subject = subject, Data = data };
        }

        public override SlackMessage GetSlackEventNotification(PersistentEvent ev, Project project, bool isCritical, bool isNew, bool isRegression) {
            string messageOrSource = !String.IsNullOrEmpty(ev.Message) ? ev.Message : ev.Source;
            if (String.IsNullOrEmpty(messageOrSource))
                return null;

            string notificationType = "Occurrence event";
            if (isNew)
                notificationType = "New event";
            else if (isRegression)
                notificationType = "Regression event";

            if (isCritical)
                notificationType = String.Concat("Critical ", notificationType.ToLowerInvariant());

            var attachment = new SlackMessage.SlackAttachment(ev);
            if (!String.IsNullOrEmpty(ev.Message))
                attachment.Fields.Add(new SlackMessage.SlackAttachmentFields { Title = "Message", Value = ev.Message.Truncate(60) });

            if (!String.IsNullOrEmpty(ev.Source))
                attachment.Fields.Add(new SlackMessage.SlackAttachmentFields { Title = "Source", Value = ev.Source.Truncate(60) });

            AddDefaultSlackFields(ev, attachment.Fields);
            string subject = $"[{project.Name}] A {notificationType}: *{GetSlackEventUrl(ev.Id, messageOrSource.Truncate(120))}*";
            return new SlackMessage(subject) {
                Attachments = new List<SlackMessage.SlackAttachment> { attachment }
            };
        }
    }
}