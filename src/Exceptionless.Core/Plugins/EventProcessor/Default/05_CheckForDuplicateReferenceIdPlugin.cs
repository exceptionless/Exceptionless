using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Foundatio.Caching;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(5)]
    public sealed class CheckForDuplicateReferenceIdPlugin : EventProcessorPluginBase {
        private readonly ICacheClient _cacheClient;

        public CheckForDuplicateReferenceIdPlugin(ICacheClient cacheClient, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _cacheClient = cacheClient;
        }

        public override async Task EventProcessingAsync(EventContext context) {
            if (String.IsNullOrEmpty(context.Event.ReferenceId))
                return;

            if (await _cacheClient.AddAsync(GetCacheKey(context), true, TimeSpan.FromDays(1)).AnyContext()) {
                context.SetProperty("AddedReferenceId", true);
                return;
            }

            _logger.LogWarning("Discarding event due to duplicate reference id: {ReferenceId}", context.Event.ReferenceId);
            context.IsCancelled = true;
        }

        public override Task EventBatchProcessedAsync(ICollection<EventContext> contexts) {
            var values = contexts.Where(c => !String.IsNullOrEmpty(c.Event.ReferenceId) && c.GetProperty("AddedReferenceId") == null).ToDictionary(GetCacheKey, v => true);
            if (values.Count == 0)
                return Task.CompletedTask;

            return _cacheClient.SetAllAsync(values, TimeSpan.FromDays(1));
        }

        private string GetCacheKey(EventContext context) {
            return String.Concat("Project:", context.Project.Id, ":", context.Event.ReferenceId);
        }
    }
}