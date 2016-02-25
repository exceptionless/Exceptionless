using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using RazorSharpEmail;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(20)]
    public class ErrorFormattingPlugin : FormattingPluginBase {
        private readonly IEmailGenerator _emailGenerator;

        public ErrorFormattingPlugin(IEmailGenerator emailGenerator) {
            _emailGenerator = emailGenerator;
        }

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
            
            var data = new Dictionary<string, object> { { "Title", stack.Title } };
            string value;
            if (stack.SignatureInfo.TryGetValue("ExceptionType", out value) && !String.IsNullOrEmpty(value)) {
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

        public override MailMessage GetEventNotificationMailMessage(EventNotification model) {
            if (!ShouldHandle(model.Event))
                return null;

            var error = model.Event.GetError();
            var stackingTarget = error?.GetStackingTarget();
            if (stackingTarget?.Error == null)
                return null;

            var requestInfo = model.Event.GetRequestInfo();
            string errorType = !String.IsNullOrEmpty(stackingTarget.Error.Type) ? stackingTarget.Error.Type : "Error";

            string notificationType = String.Concat(errorType, " occurrence");
            if (model.IsNew)
                notificationType = String.Concat(!model.IsCritical ? "New " : "new ", error.Type);
            else if (model.IsRegression)
                notificationType = String.Concat(errorType, " regression");

            if (model.IsCritical)
                notificationType = String.Concat("Critical ", notificationType);

            var mailerModel = new EventNotificationModel(model) {
                BaseUrl = Settings.Current.BaseURL,
                Subject = String.Concat(notificationType, ": ", stackingTarget.Error.Message.Truncate(120)),
                Url = requestInfo != null ? requestInfo.GetFullPath(true, true, true) : null,
                Message = stackingTarget.Error.Message,
                TypeFullName = errorType,
                MethodFullName = stackingTarget.Method != null ? stackingTarget.Method.GetFullName() : null
            };

            return _emailGenerator.GenerateMessage(mailerModel, "NoticeError").ToMailMessage();
        }

        public override string GetEventViewName(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return "Event-Error";
        }
    }
}
