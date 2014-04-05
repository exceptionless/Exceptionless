#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Models {
    public class Event 
#if !EMBEDDED
        : IOwnedByOrganization
#endif
    {
        public Event() {
            Tags = new TagSet();
            Data = new DataDictionary();
        }

#if !EMBEDDED
        /// <summary>
        /// Unique id that identifies an event.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The organization that the event belongs to.
        /// </summary>
        public string OrganizationId { get; set; }

        /// <summary>
        /// The project that the event belongs to.
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// The stack that the event belongs to.
        /// </summary>
        public string StackId { get; set; }
#endif

        /// <summary>
        /// The event type (ie. error, log message, feature usage).
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The event source (ie. machine name, log name).
        /// </summary>
        public string Source { get; set; }

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
        /// Optional data entries that contain additional information about this event.
        /// </summary>
        public DataDictionary Data { get; set; }

        /// <summary>
        /// An optional client generated unique identifier to be used for referencing this event instance at a later time.
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// A unique id that identifies a usage session that this event belongs to.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Marks the event as being a critical occurrence.
        /// </summary>
        public void MarkAsCritical() {
            if (Tags == null)
                Tags = new TagSet();

            Tags.Add("Critical");
        }

#if !EMBEDDED
        /// <summary>
        /// Wether the error has been marked as fixed or not.
        /// </summary>
        public bool IsFixed { get; set; }

        /// <summary>
        /// Wether the error has been marked as hidden or not.
        /// </summary>
        public bool IsHidden { get; set; }
#endif
    }
}