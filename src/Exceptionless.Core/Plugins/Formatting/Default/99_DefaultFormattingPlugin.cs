using System;
using System.Collections.Generic;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using RazorSharpEmail;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(99)]
    public sealed class DefaultFormattingPlugin : FormattingPluginBase {
        private readonly IEmailGenerator _emailGenerator;

        public DefaultFormattingPlugin(IEmailGenerator emailGenerator) {
            _emailGenerator = emailGenerator;
        }

        public override string GetStackTitle(PersistentEvent ev) {
            if (String.IsNullOrWhiteSpace(ev.Message) && ev.IsError())
                return "Unknown Error";

            return ev.Message ?? ev.Source ?? $"{ev.Type} Event".TrimStart();
        }

        public override SummaryData GetStackSummaryData(Stack stack) {
            var data = new Dictionary<string, object> { { "Type", stack.Type } };

            if (stack.SignatureInfo.TryGetValue("Source", out string value))
                data.Add("Source", value);

            return new SummaryData { TemplateKey = "stack-summary", Data = data };
        }

        public override SummaryData GetEventSummaryData(PersistentEvent ev) {
            var data = new Dictionary<string, object> {
                { "Message", GetStackTitle(ev) },
                { "Source", ev.Source },
                { "Type", ev.Type }
            };

            AddUserIdentitySummaryData(data, ev.GetUserIdentity());

            return new SummaryData { TemplateKey = "event-summary", Data = data };
        }

        public override MailMessage GetEventNotificationMailMessage(EventNotification model) {
            string messageOrSource = !String.IsNullOrEmpty(model.Event.Message) ? model.Event.Message : model.Event.Source;
            if (String.IsNullOrEmpty(messageOrSource))
                return null;

            string notificationType = "Occurrence event";
            if (model.IsNew)
                notificationType = "New event";
            else if (model.IsRegression)
                notificationType = "Regression event";

            if (model.IsCritical)
                notificationType = String.Concat("Critical ", notificationType.ToLowerInvariant());

            var requestInfo = model.Event.GetRequestInfo();
            var mailerModel = new EventNotificationModel(model) {
                BaseUrl = Settings.Current.BaseURL,
                Subject = String.Concat(notificationType, ": ", messageOrSource.Truncate(120)),
                Message = model.Event.Message,
                Source = model.Event.Source,
                Url = requestInfo?.GetFullPath(true, true, true)
            };

            return _emailGenerator.GenerateMessage(mailerModel, "Notice").ToMailMessage();
        }

        public override string GetEventViewName(PersistentEvent ev) {
            return "Event";
        }
    }
}
