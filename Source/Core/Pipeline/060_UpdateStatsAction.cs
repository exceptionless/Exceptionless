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
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;

namespace Exceptionless.Core.Pipeline {
    [Priority(60)]
    public class UpdateStatsAction : EventPipelineActionBase {
        private readonly IStackRepository _stackRepository;

        public UpdateStatsAction(IStackRepository stackRepository) {
            _stackRepository = stackRepository;
        }

        protected override bool IsCritical { get { return true; } }

        public override void Process(EventContext ctx) {}

        public override void ProcessBatch(ICollection<EventContext> contexts) {
            var stacks = contexts.Where(c => !c.IsNew).GroupBy(c => c.Event.StackId);
            foreach (var stack in stacks)
                _stackRepository.IncrementEventCounter(stack.First().Event.OrganizationId, stack.Key, stack.Min(s => s.Event.Date.UtcDateTime), stack.Max(s => s.Event.Date.UtcDateTime), stack.Count());
        }
    }
}