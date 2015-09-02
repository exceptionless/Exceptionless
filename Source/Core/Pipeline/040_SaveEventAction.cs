﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;

namespace Exceptionless.Core.Pipeline {
    [Priority(40)]
    public class SaveEventAction : EventPipelineActionBase {
        private readonly IEventRepository _eventRepository;

        public SaveEventAction(IEventRepository eventRepository) {
            _eventRepository = eventRepository;
        }

        protected override bool IsCritical { get { return true; } }

        public override async Task ProcessBatchAsync(ICollection<EventContext> contexts) {
            try {
                _eventRepository.Add(contexts.Select(c => c.Event).ToList());
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
            return Task.FromResult(0);
        }
    }
}