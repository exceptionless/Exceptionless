using System;
using System.Collections.Generic;
using System.Linq;
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
                data.Add("Type", value.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last());
                data.Add("TypeFullName", value);
            }

            if (stack.SignatureInfo.TryGetValue("Method", out value) && !String.IsNullOrEmpty(value)) {
                string method = value.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last();
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
                data.Add("Type", stackingTarget.Error.Type.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last());
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

        public override Dictionary<string, object> GetEventNotificationMailMessageData(PersistentEvent ev, bool isCritical, bool isNew, bool isRegression) {
            if (!ShouldHandle(ev))
                return null;

            var error = ev.GetError();
            var stackingTarget = error?.GetStackingTarget();
            if (stackingTarget?.Error == null)
                return null;

            string errorType = !String.IsNullOrEmpty(stackingTarget.Error.Type) ? stackingTarget.Error.Type : "Error";
            string notificationType = String.Concat(errorType, " occurrence");
            if (isNew)
                notificationType = String.Concat(!isCritical ? "New " : "new ", errorType);
            else if (isRegression)
                notificationType = String.Concat(errorType, " regression");

            if (isCritical)
                notificationType = String.Concat("Critical ", notificationType);

            var fields = new Dictionary<string, object> { { "Message", stackingTarget.Error.Message } };
            if (!String.IsNullOrEmpty(stackingTarget.Error.Type))
                fields.Add("Type", stackingTarget.Error.Type);

            if (stackingTarget.Method != null)
                fields.Add("Method", stackingTarget.Method.GetFullName());

            var requestInfo = ev.GetRequestInfo();
            if (requestInfo != null)
                fields.Add("Url", requestInfo.GetFullPath(true, true, true));

            return new Dictionary<string, object> {
                { "Subject", String.Concat(notificationType, ": ", stackingTarget.Error.Message.Truncate(120)) },
                { "IsFixable", true },
                { "Fields", fields }
            };
        }
    }
}
