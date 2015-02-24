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
    [Priority(40)]
    public class SaveEventAction : EventPipelineActionBase {
        private readonly IEventRepository _eventRepository;

        public SaveEventAction(IEventRepository eventRepository) {
            _eventRepository = eventRepository;
        }

        protected override bool IsCritical { get { return true; } }

        public override void ProcessBatch(ICollection<EventContext> contexts) {
            _eventRepository.Add(contexts.Select(c => c.Event).ToList());
        }

        public override void Process(EventContext ctx) {}
    }
}