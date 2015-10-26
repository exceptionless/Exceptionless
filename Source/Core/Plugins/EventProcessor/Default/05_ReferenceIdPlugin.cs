using System;
using System.Threading.Tasks;
using Exceptionless.Core.Component;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Foundatio.Caching;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(5)]
    public class ReferenceIdPlugin : EventProcessorPluginBase {
        private readonly ICacheClient _cacheClient;

        public ReferenceIdPlugin(ICacheClient cacheClient) {
            _cacheClient = cacheClient;
        }

        public override async Task EventProcessingAsync(EventContext context) {
            if (String.IsNullOrEmpty(context.Event.ReferenceId))
                return;

            string key = String.Concat(context.Project.Id, ":", context.Event.ReferenceId);
            if (!await _cacheClient.AddAsync(key, true, TimeSpan.FromMinutes(1)).AnyContext())
                context.IsCancelled = true;
        }

        public override Task EventProcessedAsync(EventContext context) {
            if (String.IsNullOrEmpty(context.Event.ReferenceId))
                return TaskHelper.Completed();

            string key = String.Concat(context.Project.Id, ":", context.Event.ReferenceId);
            return _cacheClient.SetAsync(key, true, TimeSpan.FromHours(12));
        }
    }
}