﻿using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor;

[Priority(40)]
public sealed class RequestInfoPlugin : EventProcessorPluginBase
{
    public const int MAX_VALUE_LENGTH = 1000;
    public static readonly List<string> DefaultExclusions = new()
    {
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

    public RequestInfoPlugin(UserAgentParser parser, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _parser = parser;
    }

    public override async Task EventBatchProcessingAsync(ICollection<EventContext> contexts)
    {
        var project = contexts.First().Project;
        var exclusions = DefaultExclusions.Union(project.Configuration.Settings.GetStringCollection(SettingsDictionary.KnownKeys.DataExclusions)).ToList();
        foreach (var context in contexts)
        {
            var request = context.Event.GetRequestInfo();
            if (request is null)
                continue;

            if (context.IncludePrivateInformation)
            {
                var submissionClient = context.Event.GetSubmissionClient();
                AddClientIpAddress(request, submissionClient);
            }
            else
            {
                request.ClientIpAddress = null;
                request.Cookies?.Clear();
                request.PostData = null;
                request.QueryString?.Clear();
            }

            await SetBrowserOsAndDeviceFromUserAgent(request, context).AnyContext();
            context.Event.AddRequestInfo(request.ApplyDataExclusions(exclusions, MAX_VALUE_LENGTH));
        }
    }

    private void AddClientIpAddress(RequestInfo request, SubmissionClient? submissionClient)
    {
        var ips = (request.ClientIpAddress ?? String.Empty)
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(ip => ip.Trim())
            .ToList();

        if (!String.IsNullOrEmpty(submissionClient?.IpAddress) && submissionClient.IsJavaScriptClient())
        {
            bool requestIpIsLocal = submissionClient.IpAddress.IsLocalHost();
            if (ips.Count == 0 || !requestIpIsLocal && ips.Count(ip => !ip.IsLocalHost()) == 0)
                ips.Add(submissionClient.IpAddress);
        }

        request.ClientIpAddress = ips.Distinct().ToDelimitedString();
    }

    private async Task SetBrowserOsAndDeviceFromUserAgent(RequestInfo request, EventContext context)
    {
        if (String.IsNullOrEmpty(request.UserAgent))
            return;

        var info = await _parser.ParseAsync(request.UserAgent).AnyContext();
        if (info is null)
            return;

        request.Data ??= new DataDictionary();
        if (!String.Equals(info.UA.Family, "Other"))
        {
            request.Data[RequestInfo.KnownDataKeys.Browser] = info.UA.Family;
            if (!String.IsNullOrEmpty(info.UA.Major))
            {
                request.Data[RequestInfo.KnownDataKeys.BrowserVersion] = String.Join(".", new[] { info.UA.Major, info.UA.Minor, info.UA.Patch }.Where(v => !String.IsNullOrEmpty(v)));
                request.Data[RequestInfo.KnownDataKeys.BrowserMajorVersion] = info.UA.Major;
            }
        }

        if (!String.Equals(info.Device.Family, "Other"))
            request.Data[RequestInfo.KnownDataKeys.Device] = info.Device.Family;

        if (!String.Equals(info.OS.Family, "Other"))
        {
            request.Data[RequestInfo.KnownDataKeys.OS] = info.OS.Family;
            if (!String.IsNullOrEmpty(info.OS.Major))
            {
                request.Data[RequestInfo.KnownDataKeys.OSVersion] = String.Join(".", new[] { info.OS.Major, info.OS.Minor, info.OS.Patch }.Where(v => !String.IsNullOrEmpty(v)));
                request.Data[RequestInfo.KnownDataKeys.OSMajorVersion] = info.OS.Major;
            }
        }

        var botPatterns = context.Project.Configuration.Settings.GetStringCollection(SettingsDictionary.KnownKeys.UserAgentBotPatterns);
        request.Data[RequestInfo.KnownDataKeys.IsBot] = info.Device.IsSpider || request.UserAgent.AnyWildcardMatches(botPatterns);
    }
}
