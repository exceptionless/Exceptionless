#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Models;

namespace Exceptionless {
    /// <summary>
    /// EventArgs derived type which holds the custom event fields
    /// </summary>
    public class EventModelEventArgs : EventArgs {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventModelEventArgs" /> class.
        /// </summary>
        /// <param name="data">The event.</param>
        public EventModelEventArgs(Event data) {
            Event = data;
        }

        /// <summary>
        /// Gets the event.
        /// </summary>
        /// <value>The event.</value>
        public Event Event { get; private set; }
    }
}