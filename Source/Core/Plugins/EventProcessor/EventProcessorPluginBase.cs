using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Component;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Plugins.EventProcessor {
    public abstract class EventProcessorPluginBase : IEventProcessorPlugin {
        protected bool ContinueOnError { get; set; }

        public virtual Task StartupAsync() {
            return TaskHelper.Completed();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
        
        public virtual bool HandleError(Exception exception, EventContext context) {
            return ContinueOnError;
        }
    }
}
