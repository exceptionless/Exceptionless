using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;

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

        public override Dictionary<string, object> GetEventNotificationMailMessage(EventNotification model) {
            if (!ShouldHandle(model.Event))
                return null;

            string notificationType = "Log Message";
            if (model.IsNew)
                notificationType = "New Log Source";
            else if (model.IsRegression)
                notificationType = "Log Regression";

            if (model.IsCritical)
                notificationType = String.Concat("Critical ", notificationType.ToLowerInvariant());

           var requestInfo = model.Event.GetRequestInfo();
            return new Dictionary<string, object> {
                { "Subject", String.Concat(notificationType, ": ", model.Event.Source.Truncate(120)) },
                { "BaseUrl", Settings.Current.BaseURL },
                { "Url", requestInfo?.GetFullPath(true, true, true) ?? model.Event.Source }
            };
        }

        public override string GetEventViewName(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return "Event-NotFound";
        }
    }
}