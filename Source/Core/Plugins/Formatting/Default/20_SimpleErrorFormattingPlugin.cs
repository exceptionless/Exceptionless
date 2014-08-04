using System;
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
    [Priority(20)]
    public class SimpleErrorFormattingPlugin : FormattingPluginBase {
        private readonly IEmailGenerator _emailGenerator;

        public SimpleErrorFormattingPlugin(IEmailGenerator emailGenerator) {
            _emailGenerator = emailGenerator;
        }

        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsError() && ev.Data.ContainsKey(Event.KnownDataKeys.SimpleError);
        }
        
        public override SummaryData GetStackSummary(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            dynamic data = GetSummaryData(ev);
            if (data == null)
                return null;

            data.StackId = ev.StackId;
            return new SummaryData(ev.Id, "stack-simple-summary", data);
        }

        public override SummaryData GetEventSummary(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            dynamic data = GetSummaryData(ev);
            if (data == null)
                return null;

            return new SummaryData(ev.Id, "event-simple-summary", data);
        }

        public override MailMessage GetEventNotificationMailMessage(EventNotification model) {
            if (!ShouldHandle(model.Event))
                return null;

            var error = model.Event.GetSimpleError();
            var requestInfo = model.Event.GetRequestInfo();

            string notificationType = String.Concat(error.Type, " Occurrence");
            if (model.IsNew)
                notificationType = String.Concat("New ", error.Type);
            else if (model.IsRegression)
                notificationType = String.Concat(error.Type, " Regression");

            if (model.IsCritical)
                notificationType = String.Concat("Critical ", notificationType);

            var mailerModel = new EventNotificationModel(model) {
                BaseUrl = Settings.Current.BaseURL,
                Subject = String.Concat(notificationType, ": ", model.Event.Message.Truncate(120)),
                Message = error.Message,
                Url = requestInfo != null ? requestInfo.GetFullPath(true, true, true) : null,
                StackTrace = error.StackTrace
            };

            return _emailGenerator.GenerateMessage(mailerModel, "Notice").ToMailMessage();
        }

        public override string GetEventViewName(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return "Event-Simple";
        }

        private dynamic GetSummaryData(PersistentEvent ev) {
            var error = ev.GetSimpleError();
            if (error == null)
                return null;

            dynamic data = new {
                Id = ev.Id,
                Message = ev.Message,
                Type = error.Type.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last(),
                TypeFullName = error.Type,
            };

            var requestInfo = ev.GetRequestInfo();
            if (requestInfo != null && !String.IsNullOrEmpty(requestInfo.Path))
                data.Path = requestInfo.Path;

            return data;
        }
    }
}