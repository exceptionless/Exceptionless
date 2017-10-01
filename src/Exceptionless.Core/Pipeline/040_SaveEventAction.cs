using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline {
    [Priority(40)]
    public class SaveEventAction : EventPipelineActionBase {
        private readonly IEventRepository _eventRepository;

        public SaveEventAction(IEventRepository eventRepository, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _eventRepository = eventRepository;
        }

        protected override bool IsCritical => true;

        public override async Task ProcessBatchAsync(ICollection<EventContext> contexts) {
            try {
                await _eventRepository.AddAsync(contexts.Select(c => c.Event).ToList()).AnyContext();
            } catch (Exception ex) {
                foreach (var context in contexts) {
                    bool cont = false;
                    try {
                        cont = HandleError(ex, context);
                    } catch {}

                    if (!cont)
                        context.SetError(ex.Message, ex);
                }
            }
        }

        public override Task ProcessAsync(EventContext ctx) {
            return Task.CompletedTask;
        }
    }
}