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

        public SummaryData GetStackSummaryData(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return new SummaryData(ev.Id, "stack-notfound-summary", new { Id = ev.Id, StackId = ev.StackId, Source = ev.Source });
        }

        public SummaryData GetEventSummaryData(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return new SummaryData(ev.Id, "event-notfound-summary", new { Id = ev.Id, Source = ev.Source });
        }

        public MailMessage GetEventNotificationMailMessage(EventNotification model) {
            if (!ShouldHandle(model.Event))
                return null;

            string notificationType = "Occurrence 404";
            if (model.IsNew)
                notificationType = "New 404";
            else if (model.IsRegression)
                notificationType = "Regression 404";

            if (model.IsCritical)
                notificationType = String.Concat("Critical ", notificationType.ToLower());

           var requestInfo = model.Event.GetRequestInfo();
            var mailerModel = new EventNotificationModel(model) {
                BaseUrl = Settings.Current.BaseURL,
                Subject = String.Concat(notificationType, ": ", model.Event.Source.Truncate(120)),
                Url = requestInfo != null ? requestInfo.GetFullPath(true, true, true) : model.Event.Source
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