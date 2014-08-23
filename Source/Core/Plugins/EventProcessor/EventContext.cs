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
using Exceptionless.Core.Utility;
using Exceptionless.Models;

namespace Exceptionless.Core.Plugins.EventProcessor {
    public class EventContext : ExtensibleObject, IPipelineContext {
        public EventContext(PersistentEvent ev) {
            Event = ev;
            StackSignatureData = new Dictionary<string, string>();
        }

        public PersistentEvent Event { get; set; }
        public Stack Stack { get; set; }
        public Project Project { get; set; }
        public Organization Organization { get; set; }
        public bool IsNew { get; set; }
        public bool IsRegression { get; set; }
        public IDictionary<string, string> StackSignatureData { get; private set; }

        public bool IsCancelled { get; set; }
        public bool IsProcessed { get; set; }
    }
}