using System;
using System.Dynamic;
using System.Linq;
using System.Text;
using CodeSmith.Core.Component;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Extensions;
using Exceptionless.Models;
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
            if (error == null)
                return null;

            return error.Message;
        }

        public override SummaryData GetStackSummaryData(Stack stack) {
            if (stack.SignatureInfo == null || !stack.SignatureInfo.ContainsKey("ExceptionType"))
                return null;

            dynamic data = new ExpandoObject();
            data.Title = stack.Title;

            string value;
            if (stack.SignatureInfo.TryGetValue("ExceptionType", out value)) {
                data.Type = value.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last();
                data.TypeFullName = value;
            }

            if (stack.SignatureInfo.TryGetValue("Method", out value)) {
                string method = value.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last();
                int index = method.IndexOf('(');
                data.Method = index > 0 ? method.Substring(0, index) : method;
                data.MethodFullName = value;
            }

            if (stack.SignatureInfo.TryGetValue("Message", out value))
                data.Message = value;

            if (stack.SignatureInfo.TryGetValue("Path", out value))
                data.Path = value;

            return new SummaryData { TemplateKey = "stack-error-summary", Data = data };
        }

        public override SummaryData GetEventSummaryData(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            var error = ev.GetError();
            if (error == null)
                return null;

            var stackingTarget = error.GetStackingTarget();
            if (stackingTarget == null)
                return null;

            dynamic data = new ExpandoObject();
            data.Id = ev.Id;
            data.Message = ev.Message;
            data.Type = stackingTarget.Error.Type.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last();
            data.TypeFullName = stackingTarget.Error.Type;

            if (stackingTarget.Method != null) {
                data.Method = stackingTarget.Method.Name;
                data.MethodFullName = stackingTarget.Method.GetFullName();
            }

            var requestInfo = ev.GetRequestInfo();
            if (requestInfo != null && !String.IsNullOrEmpty(requestInfo.Path))
                data.Path = requestInfo.Path;

            return new SummaryData { TemplateKey = "event-error-summary", Data = data };
        }

        public override MailMessage GetEventNotificationMailMessage(EventNotification model) {
            if (!ShouldHandle(model.Event))
                return null;

            var error = model.Event.GetError();
            var stackingTarget = error.GetStackingTarget();
            var requestInfo = model.Event.GetRequestInfo();

            string notificationType = String.Concat(stackingTarget.Error.Type, " occurrence");
            if (model.IsNew)
                notificationType = String.Concat(!model.IsCritical ? "New " : "new ", error.Type);
            else if (model.IsRegression)
                notificationType = String.Concat(stackingTarget.Error.Type, " regression");

            if (model.IsCritical)
                notificationType = String.Concat("Critical ", notificationType);

            var mailerModel = new EventNotificationModel(model) {
                BaseUrl = Settings.Current.BaseURL,
                Subject = String.Concat(notificationType, ": ", stackingTarget.Error.Message.Truncate(120)),
                Url = requestInfo != null ? requestInfo.GetFullPath(true, true, true) : null,
                Message = stackingTarget.Error.Message,
                TypeFullName = stackingTarget.Error.Type,
                MethodFullName = stackingTarget.Method.GetFullName(),
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