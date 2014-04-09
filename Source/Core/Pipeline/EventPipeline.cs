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
using CodeSmith.Core.Component;
using CodeSmith.Core.Dependency;
using Exceptionless.Core.EventPlugins;
using Exceptionless.Models;

namespace Exceptionless.Core.Pipeline {
    public class EventPipeline : PipelineBase<EventContext, EventPipelineActionBase> {
        public EventPipeline(IDependencyResolver dependencyResolver) : base(dependencyResolver) {}

        public void Run(Event data) {
            var ctx = new EventContext(data);
            Run(ctx);
        }

        public void Run(IEnumerable<Event> events) {
            foreach (Event e in events)
                Run(e);
        }
    }
}