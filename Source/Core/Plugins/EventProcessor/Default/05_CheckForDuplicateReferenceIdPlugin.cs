using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Foundatio.Caching;
using Foundatio.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(5)]
    public class CheckForDuplicateReferenceIdPlugin : EventProcessorPluginBase {
        private readonly ICacheClient _cacheClient;

        public CheckForDuplicateReferenceIdPlugin(ICacheClient cacheClient, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _cacheClient = cacheClient;
        }

        public override async Task EventProcessingAsync(EventContext context) {
            if (String.IsNullOrEmpty(context.Event.ReferenceId))
                return;

            // TODO: Look into using a lock on reference id so we can ensure there is no race conditions with setting keys
            if (await _cacheClient.AddAsync(GetCacheKey(context), true, TimeSpan.FromMinutes(1)).AnyContext())
                return;

            _logger.Warn().Project(context.Event.ProjectId).Message("Discarding event due to duplicate reference id: {0}", context.Event.ReferenceId).Write();
            context.IsCancelled = true;
        }
        
        public override Task EventProcessedAsync(EventContext context) {
            if (String.IsNullOrEmpty(context.Event.ReferenceId))
                return Task.CompletedTask;
            
            return _cacheClient.SetAsync(GetCacheKey(context), true, TimeSpan.FromDays(1));
        }
        
        private string GetCacheKey(EventContext context) {
            return String.Concat("project:", context.Project.Id, ":", context.Event.ReferenceId);
        }
    }
}