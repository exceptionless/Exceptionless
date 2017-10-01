using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor {
    public abstract class EventProcessorPluginBase : PluginBase, IEventProcessorPlugin {
        public EventProcessorPluginBase(ILoggerFactory loggerFactory = null) : base(loggerFactory) { }

        protected bool ContinueOnError { get; set; }

        public virtual Task StartupAsync() {
            return Task.CompletedTask;
        }

        public virtual async Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            foreach (var ctx in contexts) {
                try {
                    await EventProcessingAsync(ctx).AnyContext();
                } catch (Exception ex) {
                    bool cont = false;
                    try {
                        cont = HandleError(ex, ctx);
                    } catch { }

                    if (!cont)
                        ctx.SetError(ex.Message, ex);
                }
            }
        }

        public virtual Task EventProcessingAsync(EventContext context) {
            return Task.CompletedTask;
        }

        public virtual async Task EventBatchProcessedAsync(ICollection<EventContext> contexts) {
            foreach (var ctx in contexts) {
                try {
                    await EventProcessedAsync(ctx).AnyContext();
                } catch (Exception ex) {
                    bool cont = false;
                    try {
                        cont = HandleError(ex, ctx);
                    } catch { }

                    if (!cont)
                        ctx.SetError(ex.Message, ex);
                }
            }
        }

        public virtual Task EventProcessedAsync(EventContext context) {
            return Task.CompletedTask;
        }

        public virtual bool HandleError(Exception exception, EventContext context) {
            return ContinueOnError;
        }
    }
}
