using System;
using System.Dynamic;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(50)]
    public class SessionFormattingPlugin : FormattingPluginBase {
        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsSessionStart() || ev.IsSessionEnd();
        }
        
        public override SummaryData GetStackSummaryData(Stack stack) {
            if (!stack.SignatureInfo.ContainsKeyWithValue("Type", Event.KnownTypes.SessionStart, Event.KnownTypes.SessionEnd))
                return null;

            return new SummaryData { TemplateKey = "stack-session-summary", Data = new { Title = stack.Title } };
        }

        public override string GetStackTitle(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return ev.IsSessionStart() ? "Session Start" : "Session End";
        }

        public override SummaryData GetEventSummaryData(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;


            dynamic data = new ExpandoObject();
            data.SessionId = ev.SessionId;

            if (ev.IsSessionStart()) {
                data.Value = ev.Value.GetValueOrDefault();

                DateTime? endTime = ev.GetSessionEndTime();
                if (endTime.HasValue)
                    data.SessionEnd = endTime;
            }

            var identity = ev.GetUserIdentity();
            if (identity != null) {
                if (!String.IsNullOrEmpty(identity.Identity))
                    data.Identity = identity.Identity;
                
                if (!String.IsNullOrEmpty(identity.Name))
                    data.Name = identity.Name;
            }

            return new SummaryData { TemplateKey = "event-session-summary", Data = data };
        }

        public override string GetEventViewName(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            return "Event-Session";
        }
    }
}