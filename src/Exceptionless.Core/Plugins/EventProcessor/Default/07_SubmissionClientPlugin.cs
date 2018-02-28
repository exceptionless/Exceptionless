using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(7)]
    public sealed class SubmissionClientPlugin : EventProcessorPluginBase {
        public SubmissionClientPlugin(ILoggerFactory loggerFactory = null) : base(loggerFactory) { }

        public override Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            contexts.ForEach(c => c.Event.Data.Remove(Event.KnownDataKeys.SubmissionClient));

            var epi = contexts.FirstOrDefault()?.EventPostInfo;
            if (epi == null)
                return Task.CompletedTask;

            bool hasIpAddress = !String.IsNullOrEmpty(epi.IpAddress);
            bool hasUserAgent = !String.IsNullOrEmpty(epi.UserAgent);
            if (!hasIpAddress && !hasUserAgent)
                return Task.CompletedTask;

            var submissionClient = new SubmissionClient();
            if (hasIpAddress)
                submissionClient.IpAddress = !epi.IpAddress.IsLocalHost() ? epi.IpAddress.Trim() : "127.0.0.1";

            if (hasUserAgent) {
                var parts = epi.UserAgent.Split(new [] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && Version.TryParse(parts[1], out var version)) {
                    submissionClient.UserAgent = parts[0].Trim().ToLowerInvariant();
                    submissionClient.Version = version.ToString();
                } else {
                    submissionClient.UserAgent = epi.UserAgent.Trim().ToLowerInvariant();
                }
            }

            contexts.ForEach(c => c.Event.SetSubmissionClient(submissionClient));
            return Task.CompletedTask;
        }
    }
}
