using System;
using System.Dynamic;
using System.Linq;
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
    [Priority(10)]
    public class SimpleErrorFormattingPlugin : FormattingPluginBase {
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

            dynamic data = new ExpandoObject();
            data.Title = stack.Title;

            string value;
            if (stack.SignatureInfo.TryGetValue("ExceptionType", out value)) {
                data.Type = value.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last();
                data.TypeFullName = value;
            }

            if (stack.SignatureInfo.TryGetValue("Path", out value))
                data.Path = value;

            return new SummaryData { TemplateKey = "stack-simple-summary",  Data = data };
        }

        public override string GetStackTitle(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            var error = ev.GetSimpleError();
            if (error == null)
                return null;

            return error.Message;
        }

        public override SummaryData GetEventSummaryData(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            var error = ev.GetSimpleError();
            if (error == null)
                return null;

            dynamic data = new ExpandoObject();
            data.Message = ev.Message;
            data.Type = error.Type.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last();
            data.TypeFullName = error.Type;

            var requestInfo = ev.GetRequestInfo();
            if (requestInfo != null && !String.IsNullOrEmpty(requestInfo.Path))
                data.Path = requestInfo.Path;

            return new SummaryData { TemplateKey = "event-simple-summary", Data = data };
        }

        public override MailMessage GetEventNotificationMailMessage(EventNotification model) {
            if (!ShouldHandle(model.Event))
                return null;

            var error = model.Event.GetSimpleError();
            var requestInfo = model.Event.GetRequestInfo();

            string notificationType = String.Concat(error.Type, " Occurrence");
            if (model.IsNew)
                notificationType = String.Concat(!model.IsCritical ? "New " : "new ", error.Type);
            else if (model.IsRegression)
                notificationType = String.Concat(error.Type, " Regression");

            if (model.IsCritical)
                notificationType = String.Concat("Critical ", notificationType);

            var mailerModel = new EventNotificationModel(model) {
                BaseUrl = Settings.Current.BaseURL,
                Subject = String.Concat(notificationType, ": ", error.Message.Truncate(120)),
                Message = error.Message,
                Url = requestInfo != null ? requestInfo.GetFullPath(true, true, true) : null
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