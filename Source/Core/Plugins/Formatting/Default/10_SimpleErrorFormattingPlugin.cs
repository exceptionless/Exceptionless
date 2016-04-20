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
    [Priority(10)]
    public sealed class SimpleErrorFormattingPlugin : FormattingPluginBase {
        private readonly IEmailGenerator _emailGenerator;

        public SimpleErrorFormattingPlugin(IEmailGenerator emailGenerator) {
            _emailGenerator = emailGenerator;
        }

        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsError() && ev.Data.ContainsKey(Event.KnownDataKeys.SimpleError);
        }

        public override SummaryData GetStackSummaryData(Stack stack) {
            if (stack.SignatureInfo == null || !stack.SignatureInfo.ContainsKey("StackTrace"))
                return null;
            
            var data = new Dictionary<string, object> { { "Title", stack.Title } };
            string value;
            if (stack.SignatureInfo.TryGetValue("ExceptionType", out value)) {
                data.Add("Type", value.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last());
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
                data.Add("Type", error.Type.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last());
                data.Add("TypeFullName", error.Type);
            }

            var requestInfo = ev.GetRequestInfo();
            if (!String.IsNullOrEmpty(requestInfo?.Path))
                data.Add("Path", requestInfo.Path);

            return new SummaryData { TemplateKey = "event-simple-summary", Data = data };
        }

        public override MailMessage GetEventNotificationMailMessage(EventNotification model) {
            if (!ShouldHandle(model.Event))
                return null;

            var error = model.Event.GetSimpleError();
            if (error == null)
                return null;

            var requestInfo = model.Event.GetRequestInfo();
            string errorType = !String.IsNullOrEmpty(error.Type) ? error.Type : "Error";

            string notificationType = String.Concat(errorType, " Occurrence");
            if (model.IsNew)
                notificationType = String.Concat(!model.IsCritical ? "New " : "new ", errorType);
            else if (model.IsRegression)
                notificationType = String.Concat(errorType, " Regression");

            if (model.IsCritical)
                notificationType = String.Concat("Critical ", notificationType);

            var mailerModel = new EventNotificationModel(model) {
                BaseUrl = Settings.Current.BaseURL,
                Subject = String.Concat(notificationType, ": ", error.Message.Truncate(120)),
                Message = error.Message,
                Url = requestInfo?.GetFullPath(true, true, true)
            };

            return _emailGenerator.GenerateMessage(mailerModel, "NoticeError").ToMailMessage();
        }

        public override string GetEventViewName(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return "Event-Simple";
        }
    }
}
