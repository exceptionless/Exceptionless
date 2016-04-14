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
    [Priority(60)]
    public sealed class LogFormattingPlugin : FormattingPluginBase {
        private readonly IEmailGenerator _emailGenerator;

        public LogFormattingPlugin(IEmailGenerator emailGenerator) {
            _emailGenerator = emailGenerator;
        }

        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsLog();
        }

        public override SummaryData GetStackSummaryData(Stack stack) {
            if (!stack.SignatureInfo.ContainsKeyWithValue("Type", Event.KnownTypes.Log))
                return null;

            var data = new Dictionary<string, object>();
            string source = stack.SignatureInfo?.GetString("Source");
            if (String.IsNullOrWhiteSpace(source) || !String.Equals(source, stack.Title)) {
                data.Add("Title", stack.Title);
            } else {
                data.Add("Source", source);

                string truncatedSource = source.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last();
                if (!String.Equals(source, truncatedSource))
                    data.Add("SourceShortName", truncatedSource);
            }

            return new SummaryData { TemplateKey = "stack-log-summary", Data = data };
        }

        public override string GetStackTitle(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return !String.IsNullOrEmpty(ev.Source) ? ev.Source : "(Global)";
        }

        public override SummaryData GetEventSummaryData(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            var data = new Dictionary<string, object> { { "Message", ev.Message } };
            AddUserIdentitySummaryData(data, ev.GetUserIdentity());

            if (!String.IsNullOrWhiteSpace(ev.Source)) {
                data.Add("Source", ev.Source);

                string truncatedSource = ev.Source.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last();
                if (!String.Equals(ev.Source, truncatedSource))
                    data.Add("SourceShortName", truncatedSource);
            }

            object temp;
            string level = ev.Data.TryGetValue(Event.KnownDataKeys.Level, out temp) ? temp as string : null;
            if (!String.IsNullOrWhiteSpace(level))
                data.Add("Level", level.Trim());

            return new SummaryData { TemplateKey = "event-log-summary", Data = data };
        }

        public override MailMessage GetEventNotificationMailMessage(EventNotification model) {
            if (!ShouldHandle(model.Event))
                return null;

            string notificationType = "Log Message";
            if (model.IsNew)
                notificationType = "New Log Source";
            else if (model.IsRegression)
                notificationType = "Log Regression";

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