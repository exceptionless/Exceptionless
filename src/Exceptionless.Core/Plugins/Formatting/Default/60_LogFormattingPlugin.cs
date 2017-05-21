using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(60)]
    public sealed class LogFormattingPlugin : FormattingPluginBase {
        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsLog();
        }

        public override SummaryData GetStackSummaryData(Stack stack) {
            if (!stack.SignatureInfo.ContainsKeyWithValue("Type", Event.KnownTypes.Log))
                return null;

            var data = new Dictionary<string, object>();
            string source = stack.SignatureInfo?.GetString("Source");
            if (!String.IsNullOrWhiteSpace(source) && String.Equals(source, stack.Title)) {
                var parts = source.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1 && !String.Equals(source, parts.Last()) && parts.All(p => p.IsValidIdentifier())) {
                    data.Add("Source", source);
                    data.Add("SourceShortName", parts.Last());
                }
            }

            return new SummaryData { TemplateKey = "stack-log-summary", Data = data };
        }

        public override string GetStackTitle(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return !String.IsNullOrEmpty(ev.Source) ? ev.Source : "(Global)";
        }

        public override SummaryData GetEventSummaryData(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            var data = new Dictionary<string, object> { { "Message", ev.Message } };
            AddUserIdentitySummaryData(data, ev.GetUserIdentity());

            if (!String.IsNullOrWhiteSpace(ev.Source)) {
                data.Add("Source", ev.Source);

                var parts = ev.Source.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1 && !String.Equals(ev.Source, parts.Last()) && parts.All(p => p.IsValidIdentifier()))
                    data.Add("SourceShortName", parts.Last());
            }

            string level = ev.Data.TryGetValue(Event.KnownDataKeys.Level, out object temp) ? temp as string : null;
            if (!String.IsNullOrWhiteSpace(level))
                data.Add("Level", level.Trim());

            return new SummaryData { TemplateKey = "event-log-summary", Data = data };
        }

        public override MailMessageData GetEventNotificationMailMessageData(PersistentEvent ev, bool isCritical, bool isNew, bool isRegression) {
            if (!ShouldHandle(ev))
                return null;

            string notificationType = "Log message";
            if (isNew)
                notificationType = "New log source";
            else if (isRegression)
                notificationType = "Log regression";

            if (isCritical)
                notificationType = String.Concat("Critical ", notificationType.ToLowerInvariant());

            string source = !String.IsNullOrEmpty(ev.Source) ? ev.Source : "(Global)";
            string subject = String.Concat(notificationType, ": ", source).Truncate(120);
            var data = new Dictionary<string, object> { { "Source", source.Truncate(60) } };
            if (!String.IsNullOrEmpty(ev.Message))
                data.Add("Message", ev.Message.Truncate(60));

            string level = ev.GetLevel();
            if (!String.IsNullOrEmpty(level))
                data.Add("Level", level.Truncate(60));

            var requestInfo = ev.GetRequestInfo();
            if (requestInfo != null)
                data.Add("Url", requestInfo.GetFullPath(true, true, true));

            return new MailMessageData { Subject = subject, Data = data };
        }

        public override SlackMessage GetSlackEventNotification(PersistentEvent ev, Project project, bool isCritical, bool isNew, bool isRegression) {
            if (!ShouldHandle(ev))
                return null;

            string notificationType = "log message";
            if (isNew)
                notificationType = "new log source";
            else if (isRegression)
                notificationType = "log regression";

            if (isCritical)
                notificationType = String.Concat("critical ", notificationType);

            string source = !String.IsNullOrEmpty(ev.Source) ? ev.Source : "(Global)";
            var attachment = new SlackMessage.SlackAttachment(ev) {
                Fields = new List<SlackMessage.SlackAttachmentFields> {
                    new SlackMessage.SlackAttachmentFields {
                        Title = "Source",
                        Value = source.Truncate(60)
                    }
                }
            };

            if (!String.IsNullOrEmpty(ev.Message))
                attachment.Fields.Add(new SlackMessage.SlackAttachmentFields { Title = "Message", Value = ev.Message.Truncate(60) });

            string level = ev.GetLevel();
            if (!String.IsNullOrEmpty(level)) {
                switch (level.ToLower()) {
                    case "trace":
                    case "debug":
                        attachment.Color = "#5cb85c";
                        break;
                    case "info":
                        attachment.Color = "#5bc0de";
                        break;
                    case "warn":
                        attachment.Color = "#f0ad4e";
                        break;
                    case "error":
                    case "fatal":
                        attachment.Color = "#d9534f";
                        break;
                }

                attachment.Fields.Add(new SlackMessage.SlackAttachmentFields { Title = "Level", Value = level.Truncate(60) });
            }

            var requestInfo = ev.GetRequestInfo();
            if (requestInfo != null)
                attachment.Fields.Add(new SlackMessage.SlackAttachmentFields { Title = "Url", Value = requestInfo.GetFullPath(true, true, true) });

            AddDefaultSlackFields(ev, attachment.Fields);
            string subject = $"[{project.Name}] A {notificationType}: *{GetSlackEventUrl(ev.Id, source.Truncate(120))}*";
            return new SlackMessage(subject) {
                Attachments = new List<SlackMessage.SlackAttachment> { attachment }
            };
        }
    }
}