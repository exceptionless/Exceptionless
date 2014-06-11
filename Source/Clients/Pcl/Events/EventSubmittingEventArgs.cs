#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless {
    public class EventSubmittingEventArgs : EventArgs {
        public EventSubmittingEventArgs(Event data, IDictionary<string, object> enrichmentContextData) {
            Event = data;
            EnrichmentContextData = enrichmentContextData;
        }

        public Event Event { get; private set; }
        
        /// <summary>
        /// Any contextual data objects to be used by Exceptionless enrichments to gather default
        /// information to add to the event data.
        /// </summary>
        public IDictionary<string, object> EnrichmentContextData { get; private set; }

        /// <summary>
        /// Wether the event should be canceled.
        /// </summary>
        public bool Cancel { get; set; }
    }
}