﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(45)]
    public sealed class EnvironmentInfoPlugin : EventProcessorPluginBase {
        public EnvironmentInfoPlugin(IOptions<AppOptions> options, ILoggerFactory loggerFactory = null) : base(options, loggerFactory) { }

        public override Task EventProcessingAsync(EventContext context) {
            var environment = context.Event.GetEnvironmentInfo();
            if (environment == null)
                return Task.CompletedTask;

            if (context.IncludePrivateInformation) {
                var submissionClient = context.Event.GetSubmissionClient();
                AddClientIpAddress(environment, submissionClient);
            } else {
                environment.IpAddress = null;
                environment.MachineName = null;
            }

            context.Event.SetEnvironmentInfo(environment);
            return Task.CompletedTask;
        }

        private void AddClientIpAddress(EnvironmentInfo environment, SubmissionClient submissionClient) {
            var ips = (environment.IpAddress ?? String.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ip => ip.Trim())
                .ToList();

            if (!String.IsNullOrEmpty(submissionClient?.IpAddress) && !submissionClient.IsJavaScriptClient()) {
                bool requestIpIsLocal = submissionClient.IpAddress.IsLocalHost();
                if (ips.Count == 0 || !requestIpIsLocal && ips.Count(ip => !ip.IsLocalHost()) == 0)
                    ips.Add(submissionClient.IpAddress);
            }

            environment.IpAddress = ips.Distinct().ToDelimitedString();
        }
    }
}
