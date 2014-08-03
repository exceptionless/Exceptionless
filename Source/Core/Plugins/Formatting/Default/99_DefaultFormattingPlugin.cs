using System;
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
    [Priority(99)]
    public class DefaultFormattingPlugin : IFormattingPlugin {
        private readonly IEmailGenerator _emailGenerator;

        public DefaultFormattingPlugin(IEmailGenerator emailGenerator) {
            _emailGenerator = emailGenerator;
        }

        public string GetStackTitle(PersistentEvent ev) {
            return ev.Message ?? "None";
        }

        public SummaryData GetStackSummary(PersistentEvent ev) {
            return new SummaryData(ev.Id, "stack-summary", new { Id = ev.Id, StackId = ev.StackId, Message = ev.Message ?? "None" });
        }

        public SummaryData GetEventSummary(PersistentEvent ev) {
            return new SummaryData(ev.Id, "event-summary", new { Id = ev.Id, Message = ev.Message ?? "None" });
        }

        public MailMessage GetEventNotificationMailMessage(EventNotification model) {
            var requestInfo = model.Event.GetRequestInfo();
            string notificationType = String.Concat(model.Event.Message, " Occurrence");
            if (model.IsNew)
                notificationType = String.Concat("New ", model.Event.Message);
            else if (model.IsRegression)
                notificationType = String.Concat(model.Event.Message, " Regression");

            if (model.IsCritical)
                notificationType = String.Concat("Critical ", notificationType);

            var mailerModel = new EventNotificationModel(model) {
                BaseUrl = Settings.Current.BaseURL,
                Subject = String.Concat(notificationType, ": ", model.Event.Message.Truncate(120)),
                Message =  model.Event.Message,
                Url = requestInfo != null ? requestInfo.GetFullPath(true, true, true) : null
            };

            return _emailGenerator.GenerateMessage(mailerModel, "Notice").ToMailMessage();
        }

        public string GetEventViewName(PersistentEvent ev) {
            return "Event";
        }
    }
}
