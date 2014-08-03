using System;
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
    [Priority(10)]
    public class ErrorFormattingPlugin : FormattingPluginBase {
        private readonly IEmailGenerator _emailGenerator;

        public ErrorFormattingPlugin(IEmailGenerator emailGenerator) {
            _emailGenerator = emailGenerator;
        }

        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsError() && ev.Data.ContainsKey(Event.KnownDataKeys.Error);
        }

        public override SummaryData GetStackSummary(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            dynamic data = GetSummaryData(ev);
            if (data == null)
                return null;

            data.StackId = ev.StackId;
            return new SummaryData(ev.Id, "stack-error-summary", data);
        }

        public override SummaryData GetEventSummary(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            dynamic data = GetSummaryData(ev);
            if (data == null)
                return null;

            return new SummaryData(ev.Id, "event-error-summary", data);
        }

        public override MailMessage GetEventNotificationMailMessage(EventNotification model) {
            if (!ShouldHandle(model.Event))
                return null;

            var error = model.Event.GetError();
            var stackingTarget = error.GetStackingTarget();
            var requestInfo = model.Event.GetRequestInfo();

            string notificationType = String.Concat(stackingTarget.Error.Type, " Occurrence");
            if (model.IsNew)
                notificationType = String.Concat("New ", error.Type);
            else if (model.IsRegression)
                notificationType = String.Concat(stackingTarget.Error.Type, " Regression");

            if (model.IsCritical)
                notificationType = String.Concat("Critical ", notificationType);

            var mailerModel = new EventNotificationModel(model) {
                BaseUrl = Settings.Current.BaseURL,
                Subject = String.Concat(notificationType, ": ", model.Event.Message.Truncate(120)),
                Url = requestInfo != null ? requestInfo.GetFullPath(true, true, true) : null,
                Message = stackingTarget.Error.Message,
                TypeFullName = stackingTarget.Error.Type,
                MethodFullName = stackingTarget.Method.GetFullName(),
            };

            return _emailGenerator.GenerateMessage(mailerModel, "Notice").ToMailMessage();
        }

        public override string GetEventViewName(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return "Event-Error";
        }

        private dynamic GetSummaryData(PersistentEvent ev) {
            var error = ev.GetError();
            if (error == null)
                return null;

            var stackingTarget = error.GetStackingTarget();
            if (stackingTarget == null)
                return null;

            dynamic data = new {
                Id = ev.Id,
                Message = ev.Message,
                Type = stackingTarget.Error.Type.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last(),
                TypeFullName = stackingTarget.Error.Type,
            };

            if (stackingTarget.Method != null) {
                data.Method = stackingTarget.Method.Name;
                data.MethodFullName = stackingTarget.Method.GetFullName();
            }

            var requestInfo = ev.GetRequestInfo();
            if (requestInfo != null && !String.IsNullOrEmpty(requestInfo.Path))
                data.Path = requestInfo.Path;

            return data;
        }
    }
}