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
    public class NotFoundFormattingPlugin : FormattingPluginBase {
        private readonly IEmailGenerator _emailGenerator;

        public NotFoundFormattingPlugin(IEmailGenerator emailGenerator) {
            _emailGenerator = emailGenerator;
        }

        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsNotFound();
        }

        
        public override SummaryData GetStackSummaryData(Stack stack) {
            if (!stack.SignatureInfo.ContainsKeyWithValue("Type", Event.KnownTypes.NotFound))
                return null;

            return new SummaryData { TemplateKey = "stack-notfound-summary", Data = new { Title = stack.Title } };
        }


        public override string GetStackTitle(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return !String.IsNullOrEmpty(ev.Source) ? ev.Source : "(Unknown)";
        }

        public override SummaryData GetEventSummaryData(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return new SummaryData { TemplateKey = "event-notfound-summary", Data = new { Source = ev.Source } };
        }

        public override MailMessage GetEventNotificationMailMessage(EventNotification model) {
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

        public override string GetEventViewName(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return "Event-NotFound";
        }
    }
}