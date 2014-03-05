#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Models {
    public class Event {
        public Event() {
            Tags = new TagSet();
            Data = new DataDictionary();
        }

        /// <summary>
        /// The event type (ie. error, log message, feature usage).
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The date that the event occurred on.
        /// </summary>
        public DateTimeOffset Date { get; set; }

        /// <summary>
        /// A list of tags used to categorize this event.
        /// </summary>
        public TagSet Tags { get; set; }

        /// <summary>
        /// The event message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Data entries that contain additional information about this event.
        /// </summary>
        public DataDictionary Data { get; set; }

        /// <summary>
        /// A client generated unique identifier to be used for referencing this event instance at a later time.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// A unique id that identifies a usage session.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Information about the Exceptionless client that collected the event.
        /// </summary>
        public ExceptionlessClientInfo Client { get; set; }
    }
}