#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Enrichments;
using Exceptionless.Models;

namespace Exceptionless {
    public class EventSubmittingEventArgs : EventArgs {
        public EventSubmittingEventArgs(Event data, ContextData enrichmentContextData) {
            Event = data;
            EnrichmentContextData = enrichmentContextData;
        }

        public Event Event { get; private set; }
        
        /// <summary>
        /// Any contextual data objects to be used by Exceptionless enrichments to gather default
        /// information to add to the event data.
        /// </summary>
        public ContextData EnrichmentContextData { get; private set; }

        /// <summary>
        /// Wether the event should be canceled.
        /// </summary>
        public bool Cancel { get; set; }
    }
}