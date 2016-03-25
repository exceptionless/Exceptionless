﻿using System;
using System.Collections.Generic;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.Formatting {
    [Priority(50)]
    public class SessionFormattingPlugin : FormattingPluginBase {
        private bool ShouldHandle(PersistentEvent ev) {
            return ev.IsSessionStart() || ev.IsSessionEnd() || ev.IsSessionHeartbeat();
        }
        
        public override SummaryData GetStackSummaryData(Stack stack) {
            if (!stack.SignatureInfo.ContainsKeyWithValue("Type", Event.KnownTypes.Session, Event.KnownTypes.SessionEnd, Event.KnownTypes.SessionHeartbeat))
                return null;

            return new SummaryData { TemplateKey = "stack-session-summary", Data = new Dictionary<string, object> { { "Title", stack.Title } } };
        }

        public override string GetStackTitle(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;

            if (ev.IsSessionHeartbeat())
                return "Session Heartbeat";

            return ev.IsSessionStart() ? "Session Start" : "Session End";
        }

        public override SummaryData GetEventSummaryData(PersistentEvent ev) {
            if (!ShouldHandle(ev))
                return null;
            
            var data = new Dictionary<string, object> { { "SessionId", ev.GetSessionId() }, { "Type", ev.Type } };
            if (ev.IsSessionStart()) {
                data.Add("Value", ev.Value.GetValueOrDefault());

                DateTime? endTime = ev.GetSessionEndTime();
                if (endTime.HasValue)
                    data.Add("SessionEnd", endTime);
            }

            var identity = ev.GetUserIdentity();
            if (identity != null) {
                if (!String.IsNullOrEmpty(identity.Identity))
                    data.Add("Identity", identity.Identity);
                
                if (!String.IsNullOrEmpty(identity.Name))
                    data.Add("Name", identity.Name);
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