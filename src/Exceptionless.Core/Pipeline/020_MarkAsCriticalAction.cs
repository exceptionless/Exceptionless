﻿using System;
using System.Threading.Tasks;
using Exceptionless.Core.Plugins.EventProcessor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Pipeline {
    [Priority(20)]
    public class MarkAsCriticalAction : EventPipelineActionBase {
        public MarkAsCriticalAction(IOptions<AppOptions> options, ILoggerFactory loggerFactory = null) : base(options, loggerFactory) {
            ContinueOnError = true;
        }

        public override Task ProcessAsync(EventContext ctx) {
            if (ctx.Stack == null || !ctx.Stack.OccurrencesAreCritical)
                return Task.CompletedTask;

            _logger.LogTrace("Marking error as critical.");
            ctx.Event.MarkAsCritical();

            return Task.CompletedTask;
        }
    }
}