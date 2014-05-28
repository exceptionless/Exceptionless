#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using CodeSmith.Core.Component;
using Exceptionless.Core.Plugins.EventPipeline;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

namespace Exceptionless.Core.Pipeline {
    [Priority(50)]
    public class EnforceRetentionLimitsAction : EventPipelineActionBase {
        private readonly IEventRepository _eventRepository;

        public EnforceRetentionLimitsAction(IEventRepository eventRepository) {
            _eventRepository = eventRepository;
        }

        protected override bool ContinueOnError { get { return true; } }

        public override void Process(EventContext ctx) {
            if (ctx.IsNew)
                return;

            int maxEventsPerStack = ctx.Organization.MaxEventsPerMonth > 0 ? ctx.Organization.MaxEventsPerMonth + Math.Min(50, ctx.Organization.MaxEventsPerMonth * 2) : Int32.MaxValue;
            _eventRepository.RemoveOldestEvents(ctx.Event.StackId, maxEventsPerStack);
        }
    }
}