using System;
using System.Collections.Generic;
using System.Linq;
using CodeSmith.Core.Component;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(40)]
    public class RequestInfoPlugin : EventProcessorPluginBase {
        public const int MAX_VALUE_LENGTH = 1000;
        public static readonly List<string> DefaultExclusions = new List<string> {
            "*VIEWSTATE*",
            "*EVENTVALIDATION*",
            "*ASPX*",
            "__RequestVerificationToken",
            "ASP.NET_SessionId",
            "__LastErrorId"
        };

        public override void EventProcessing(EventContext context) {
            var request = context.Event.GetRequestInfo();
            if (request == null)
                return;

            var exclusions = context.Project.Configuration.Settings.ContainsKey(SettingsDictionary.KnownKeys.DataExclusions)
                    ? DefaultExclusions.Union(context.Project.Configuration.Settings.GetStringCollection(SettingsDictionary.KnownKeys.DataExclusions)).ToList()
                    : DefaultExclusions;

            context.Event.AddRequestInfo(request.ApplyDataExclusions(exclusions, MAX_VALUE_LENGTH));
        }
    }
}