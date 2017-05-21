using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(10)]
    public sealed class SimpleErrorFormattingPlugin : FormattingPluginBase {
        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsError() && ev.Data.ContainsKey(Event.KnownDataKeys.SimpleError);
        }

        public override SummaryData GetStackSummaryData(Stack stack) {
            if (stack.SignatureInfo == null || !stack.SignatureInfo.ContainsKey("StackTrace"))
                return null;

            var data = new Dictionary<string, object>();
            if (stack.SignatureInfo.TryGetValue("ExceptionType", out string value)) {
                data.Add("Type", value.TypeName());
                data.Add("TypeFullName", value);
            }

            if (stack.SignatureInfo.TryGetValue("Path", out value))
                data.Add("Path", value);

            return new SummaryData { TemplateKey = "stack-simple-summary",  Data = data };
        }

        public override string GetStackTitle(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            var error = ev.GetSimpleError();
            return error?.Message;
        }

        public override SummaryData GetEventSummaryData(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            var error = ev.GetSimpleError();
            if (error == null)
                return null;

            var data = new Dictionary<string, object> { { "Message", ev.Message } };
            AddUserIdentitySummaryData(data, ev.GetUserIdentity());

            if (!String.IsNullOrEmpty(error.Type)) {
                data.Add("Type", error.Type.TypeName());
                data.Add("TypeFullName", error.Type);
            }

            var requestInfo = ev.GetRequestInfo();
            if (!String.IsNullOrEmpty(requestInfo?.Path))
                data.Add("Path", requestInfo.Path);

            return new SummaryData { TemplateKey = "event-simple-summary", Data = data };
        }

        public override MailMessageData GetEventNotificationMailMessageData(PersistentEvent ev, bool isCritical, bool isNew, bool isRegression) {
            if (!ShouldHandle(ev))
                return null;

            var error = ev.GetSimpleError();
            if (error == null)
                return null;

            string errorTypeName = null;
            if (!String.IsNullOrEmpty(error.Type))
                errorTypeName = error.Type.TypeName().Truncate(60);

            string errorType = !String.IsNullOrEmpty(errorTypeName) ? errorTypeName : "Error";
            string notificationType = String.Concat(errorType, " occurrence");
            if (isNew)
                notificationType = String.Concat(!isCritical ? "New " : "new ", errorType);
            else if (isRegression)
                notificationType = String.Concat(errorType, " regression");

            if (isCritical)
                notificationType = String.Concat("Critical ", notificationType);

            string subject = String.Concat(notificationType, ": ", error.Message).Truncate(120);
            var data = new Dictionary<string, object> { { "Message", error.Message.Truncate(60) } };
            if (!String.IsNullOrEmpty(errorTypeName))
                data.Add("Type", errorTypeName);

            var requestInfo = ev.GetRequestInfo();
            if (requestInfo != null)
                data.Add("Url", requestInfo.GetFullPath(true, true, true));

            return new MailMessageData { Subject = subject, Data = data };
        }

        public override SlackMessage GetSlackEventNotification(PersistentEvent ev, Project project, bool isCritical, bool isNew, bool isRegression) {
            if (!ShouldHandle(ev))
                return null;

            var error = ev.GetSimpleError();
            if (error == null)
                return null;

            string errorTypeName = null;
            if (!String.IsNullOrEmpty(error.Type))
                errorTypeName = error.Type.TypeName().Truncate(60);

            string errorType = !String.IsNullOrEmpty(errorTypeName) ? errorTypeName : "error";
            string notificationType = String.Concat(errorType, " occurrence");
            if (isNew)
                notificationType = String.Concat("new ", errorType);
            else if (isRegression)
                notificationType = String.Concat(errorType, " regression");

            if (isCritical)
                notificationType = String.Concat("critical ", notificationType);

            var attachment = new SlackMessage.SlackAttachment(ev) {
                Color = "#BB423F",
                Fields = new List<SlackMessage.SlackAttachmentFields> {
                    new SlackMessage.SlackAttachmentFields {
                        Title = "Message",
                        Value = error.Message.Truncate(60)
                    }
                }
            };

            if (!String.IsNullOrEmpty(errorTypeName))
                attachment.Fields.Add(new SlackMessage.SlackAttachmentFields { Title = "Type", Value = errorTypeName });

            var lines = error.StackTrace.SplitLines().ToList();
            if (lines.Count > 0) {
                var frames = lines.Take(3).ToList();
                if (lines.Count > 3)
                    frames.Add("...");

                attachment.Fields.Add(new SlackMessage.SlackAttachmentFields { Title = "Stack Trace", Value = $"```{String.Join("\n", frames)}```" });
            }

            AddDefaultSlackFields(ev, attachment.Fields);
            string subject = $"[{project.Name}] A {notificationType}: *{GetSlackEventUrl(ev.Id, error.Message.Truncate(120))}*";
            return new SlackMessage(subject) {
                Attachments = new List<SlackMessage.SlackAttachment> { attachment }
            };
        }
    }
}
