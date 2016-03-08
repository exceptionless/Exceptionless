using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Utility;
using Foundatio.Logging;

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
            "__LastErrorId",
            "WAWebSiteID",
            "ARRAffinity"
        };

        private readonly UserAgentParser _parser;

        public RequestInfoPlugin(UserAgentParser parser, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _parser = parser;
        }

        public override async Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            var project = contexts.First().Project;
            var exclusions = project.Configuration.Settings.ContainsKey(SettingsDictionary.KnownKeys.DataExclusions)
                    ? DefaultExclusions.Union(project.Configuration.Settings.GetStringCollection(SettingsDictionary.KnownKeys.DataExclusions)).ToList()
                    : DefaultExclusions;
            
            foreach (var context in contexts) {
                var request = context.Event.GetRequestInfo();
                if (request == null)
                    continue;

                AddClientIpAddress(request, context.EventPostInfo?.IpAddress);
                await SetBrowserOsAndDeviceFromUserAgent(request, context);
                
                context.Event.AddRequestInfo(request.ApplyDataExclusions(exclusions, MAX_VALUE_LENGTH));
            }
        }

        private void AddClientIpAddress(RequestInfo request, string clientIpAddress) {
            if (String.IsNullOrEmpty(clientIpAddress))
                return;

            if (clientIpAddress.IsLocalHost())
                clientIpAddress = "127.0.0.1";

            var ips = (request.ClientIpAddress ?? String.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ip => ip.Trim())
                .Where(ip => !ip.IsLocalHost())
                .ToList();

            if (ips.Count == 0 || !clientIpAddress.IsLocalHost())
                ips.Add(clientIpAddress);

            request.ClientIpAddress = ips.Distinct().ToDelimitedString();
        }

        private async Task SetBrowserOsAndDeviceFromUserAgent(RequestInfo request, EventContext context) {
            var info = await _parser.ParseAsync(request.UserAgent, context.Project.Id).AnyContext();
            if (info != null) {
                if (!String.Equals(info.UserAgent.Family, "Other")) {
                    request.Data[RequestInfo.KnownDataKeys.Browser] = info.UserAgent.Family;
                    if (!String.IsNullOrEmpty(info.UserAgent.Major)) {
                        request.Data[RequestInfo.KnownDataKeys.BrowserVersion] = String.Join(".", new[] { info.UserAgent.Major, info.UserAgent.Minor, info.UserAgent.Patch }.Where(v => !String.IsNullOrEmpty(v)));
                        request.Data[RequestInfo.KnownDataKeys.BrowserMajorVersion] = info.UserAgent.Major;
                    }
                }

                if (!String.Equals(info.Device.Family, "Other"))
                    request.Data[RequestInfo.KnownDataKeys.Device] = info.Device.Family;

                if (!String.Equals(info.OS.Family, "Other")) {
                    request.Data[RequestInfo.KnownDataKeys.OS] = info.OS.Family;
                    if (!String.IsNullOrEmpty(info.OS.Major)) {
                        request.Data[RequestInfo.KnownDataKeys.OSVersion] = String.Join(".", new[] { info.OS.Major, info.OS.Minor, info.OS.Patch }.Where(v => !String.IsNullOrEmpty(v)));
                        request.Data[RequestInfo.KnownDataKeys.OSMajorVersion] = info.OS.Major;
                    }
                }

                var botPatterns = context.Project.Configuration.Settings.GetStringCollection(SettingsDictionary.KnownKeys.UserAgentBotPatterns).ToList();
                request.Data[RequestInfo.KnownDataKeys.IsBot] = info.Device.IsSpider || request.UserAgent.AnyWildcardMatches(botPatterns);
            }
        }
    }
}