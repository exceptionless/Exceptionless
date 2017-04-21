using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(60)]
    public sealed class LogFormattingPlugin : FormattingPluginBase {
        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsLog();
        }

        public override SummaryData GetStackSummaryData(Stack stack) {
            if (!stack.SignatureInfo.ContainsKeyWithValue("Type", Event.KnownTypes.Log))
                return null;

            var data = new Dictionary<string, object>();
            string source = stack.SignatureInfo?.GetString("Source");
            if (!String.IsNullOrWhiteSpace(source) && String.Equals(source, stack.Title)) {
                var parts = source.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1 && !String.Equals(source, parts.Last()) && parts.All(p => p.IsValidIdentifier())) {
                    data.Add("Source", source);
                    data.Add("SourceShortName", parts.Last());
                }
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

                var parts = ev.Source.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1 && !String.Equals(ev.Source, parts.Last()) && parts.All(p => p.IsValidIdentifier()))
                    data.Add("SourceShortName", parts.Last());
            }

            string level = ev.Data.TryGetValue(Event.KnownDataKeys.Level, out object temp) ? temp as string : null;
            if (!String.IsNullOrWhiteSpace(level))
                data.Add("Level", level.Trim());

            return new SummaryData { TemplateKey = "event-log-summary", Data = data };
        }

        public override MailMessageData GetEventNotificationMailMessageData(PersistentEvent ev, bool isCritical, bool isNew, bool isRegression) {
            if (!ShouldHandle(ev))
                return null;

            string notificationType = "Log Message";
            if (isNew)
                notificationType = "New Log Source";
            else if (isRegression)
                notificationType = "Log Regression";

            if (isCritical)
                notificationType = String.Concat("Critical ", notificationType.ToLowerInvariant());

            string source = !String.IsNullOrEmpty(ev.Source) ? ev.Source : "(Global)";
            string subject = String.Concat(notificationType, ": ", source.Truncate(120));
            var data = new Dictionary<string, object> { { "Source", source } };
            if (!String.IsNullOrEmpty(ev.Message))
                data.Add("Message", ev.Message);

            string level = ev.GetLevel();
            if (!String.IsNullOrEmpty(level))
                data.Add("Level", level);

            var requestInfo = ev.GetRequestInfo();
            if (requestInfo != null)
                data.Add("Url", requestInfo.GetFullPath(true, true, true));

            return new MailMessageData { Subject = subject, Data = data };
        }
    }
}