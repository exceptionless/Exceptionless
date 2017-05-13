using System;
using System.Collections.Generic;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(20)]
    public sealed class ErrorFormattingPlugin : FormattingPluginBase {
        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsError() && ev.Data.ContainsKey(Event.KnownDataKeys.Error);
        }

        public override string GetStackTitle(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            var error = ev.GetError();
            return error?.Message;
        }

        public override SummaryData GetStackSummaryData(Stack stack) {
            if (stack.SignatureInfo == null || !stack.SignatureInfo.ContainsKey("ExceptionType"))
                return null;

            var data = new Dictionary<string, object>();
            if (stack.SignatureInfo.TryGetValue("ExceptionType", out string value) && !String.IsNullOrEmpty(value)) {
                data.Add("Type", value.TypeName());
                data.Add("TypeFullName", value);
            }

            if (stack.SignatureInfo.TryGetValue("Method", out value) && !String.IsNullOrEmpty(value)) {
                string method = value.TypeName();
                int index = method.IndexOf('(');
                data.Add("Method", index > 0 ? method.Substring(0, index) : method);
                data.Add("MethodFullName", value);
            }

            if (stack.SignatureInfo.TryGetValue("Message", out value) && !String.IsNullOrEmpty(value))
                data.Add("Message", value);

            if (stack.SignatureInfo.TryGetValue("Path", out value) && !String.IsNullOrEmpty(value))
                data.Add("Path", value);

            return new SummaryData { TemplateKey = "stack-error-summary", Data = data };
        }

        public override SummaryData GetEventSummaryData(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            var stackingTarget = ev.GetStackingTarget();
            if (stackingTarget?.Error == null)
                return null;

            var data = new Dictionary<string, object> { { "Id", ev.Id }, { "Message", ev.Message } };
            AddUserIdentitySummaryData(data, ev.GetUserIdentity());

            if (!String.IsNullOrEmpty(stackingTarget.Error.Type)) {
                data.Add("Type", stackingTarget.Error.Type.TypeName());
                data.Add("TypeFullName", stackingTarget.Error.Type);
            }

            if (stackingTarget.Method != null) {
                data.Add("Method", stackingTarget.Method.Name);
                data.Add("MethodFullName", stackingTarget.Method.GetFullName());
            }

            var requestInfo = ev.GetRequestInfo();
            if (!String.IsNullOrEmpty(requestInfo?.Path))
                data.Add("Path", requestInfo.Path);

            return new SummaryData { TemplateKey = "event-error-summary", Data = data };
        }

        public override MailMessageData GetEventNotificationMailMessageData(PersistentEvent ev, bool isCritical, bool isNew, bool isRegression) {
            if (!ShouldHandle(ev))
                return null;

            var error = ev.GetError();
            var stackingTarget = error?.GetStackingTarget();
            if (stackingTarget?.Error == null)
                return null;

            string errorTypeName = null;
            if (!String.IsNullOrEmpty(stackingTarget.Error.Type))
                errorTypeName = stackingTarget.Error.Type.TypeName().Truncate(60);

            string errorType = !String.IsNullOrEmpty(errorTypeName) ? errorTypeName : "Error";
            string notificationType = String.Concat(errorType, " occurrence");
            if (isNew)
                notificationType = String.Concat(!isCritical ? "New " : "new ", errorType);
            else if (isRegression)
                notificationType = String.Concat(errorType, " regression");

            if (isCritical)
                notificationType = String.Concat("Critical ", notificationType);

            string subject = String.Concat(notificationType, ": ", stackingTarget.Error.Message).Truncate(120);
            var data = new Dictionary<string, object> { { "Message", stackingTarget.Error.Message.Truncate(60) } };
            if (!String.IsNullOrEmpty(errorTypeName))
                data.Add("Type", errorTypeName);

            if (stackingTarget.Method != null)
                data.Add("Method", stackingTarget.Method.Name.Truncate(60));

            var requestInfo = ev.GetRequestInfo();
            if (requestInfo != null)
                data.Add("Url", requestInfo.GetFullPath(true, true, true));

            return new MailMessageData { Subject = subject, Data = data };
        }

        public override SlackMessage GetSlackEventNotification(PersistentEvent ev, Project project, bool isCritical, bool isNew, bool isRegression) {
            if (!ShouldHandle(ev))
                return null;

            var error = ev.GetError();
            var stackingTarget = error?.GetStackingTarget();
            if (stackingTarget?.Error == null)
                return null;

            string errorTypeName = null;
            if (!String.IsNullOrEmpty(stackingTarget.Error.Type))
                errorTypeName = stackingTarget.Error.Type.TypeName().Truncate(60);

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
                        Value = stackingTarget.Error.Message.Truncate(60)
                    }
                }
            };

            if (!String.IsNullOrEmpty(errorTypeName))
                attachment.Fields.Add(new SlackMessage.SlackAttachmentFields { Title = "Type", Value = errorTypeName });

            if (stackingTarget.Method != null)
                attachment.Fields.Add(new SlackMessage.SlackAttachmentFields { Title = "Method", Value = stackingTarget.Method.Name.Truncate(60) });

            AddDefaultSlackFields(ev, attachment.Fields);
            string subject = $"[{project.Name}] A {notificationType}: *{GetSlackEventUrl(ev.Id, stackingTarget.Error.Message.Truncate(120))}*";
            return new SlackMessage(subject) {
                Attachments = new List<SlackMessage.SlackAttachment> { attachment }
            };
        }
    }
}
