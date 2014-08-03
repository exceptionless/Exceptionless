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
    [Priority(30)]
    public class NotFoundFormattingPlugin : IFormattingPlugin {
        private readonly IEmailGenerator _emailGenerator;

        public NotFoundFormattingPlugin(IEmailGenerator emailGenerator) {
            _emailGenerator = emailGenerator;
        }

        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsNotFound();
        }

        public string GetStackTitle(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return ev.Source;
        }

        public SummaryData GetStackSummary(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return new SummaryData(ev.Id, "stack-notfound-summary", new { Id = ev.Id, StackId = ev.StackId, Source = ev.Source });
        }

        public SummaryData GetEventSummary(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return new SummaryData(ev.Id, "event-notfound-summary", new { Id = ev.Id, Source = ev.Source });
        }

        public MailMessage GetEventNotificationMailMessage(EventNotification model) {
            if (!ShouldHandle(model.Event))
                return null;

           var requestInfo = model.Event.GetRequestInfo();
            string notificationType = String.Concat(model.Event.Source, " Occurrence");
            if (model.IsNew)
                notificationType = String.Concat("New ", model.Event.Source);
            else if (model.IsRegression)
                notificationType = String.Concat(model.Event.Source, " Regression");

            if (model.IsCritical)
                notificationType = String.Concat("Critical ", notificationType);

            var mailerModel = new EventNotificationModel(model) {
                BaseUrl = Settings.Current.BaseURL,
                Subject = String.Concat(notificationType, ": ", model.Event.Source.Truncate(120)),
                Url = requestInfo != null ? requestInfo.GetFullPath(true, true, true) : null
            };

            return _emailGenerator.GenerateMessage(mailerModel, "Notice").ToMailMessage();
        }

        public string GetEventViewName(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return "Event-NotFound";
        }
    }
}