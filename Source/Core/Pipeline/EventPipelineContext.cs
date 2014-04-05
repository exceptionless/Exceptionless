#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using CodeSmith.Core.Component;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Models;

namespace Exceptionless.Core.Pipeline {
    public class EventPipelineContext : ExtensibleObject, IPipelineContext {
        public EventPipelineContext(Event data) {
            Event = data;
        }

        public Event Event { get; set; }
        public bool IsNew { get; set; }
        public bool IsRegression { get; set; }
        public StackInfo StackInfo { get; set; }
        public StackingInfo StackingInfo { get; set; }

        public bool IsCancelled { get; set; }
        public bool IsProcessed { get; set; }
    }
}