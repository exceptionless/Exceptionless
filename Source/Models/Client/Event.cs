#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Models.Data;

namespace Exceptionless.Models {
    public class Event : IData
#if !EMBEDDED
        , IOwnedByOrganization
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

        /// <summary>
        /// The event summary html.
        /// </summary>
        public string SummaryHtml { get; set; }
#endif

        /// <summary>
        /// The event type (ie. error, log message, feature usage). Check <see cref="KnownTypes">Event.KnownTypes</see> for standard event types.
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
        /// Indicates wether the event has been marked as critical.
        /// </summary>
        public bool IsCritical {
            get { return Tags != null && Tags.Contains(KnownTags.Critical); }
        }

        /// <summary>
        /// Marks the event as being a critical occurrence.
        /// </summary>
        public void MarkAsCritical() {
            if (Tags == null)
                Tags = new TagSet();

            Tags.Add(KnownTags.Critical);
        }

        public bool IsNotFound {
            get { return Type == KnownTypes.NotFound; }
        }

        public bool IsError {
            get { return Type == KnownTypes.Error; }
        }

        public void SetError(Error error) {
            Data[KnownDataKeys.Error] = error;
        }

        public void SetRequestInfo(RequestInfo requestInfo) {
            Data[KnownDataKeys.RequestInfo] = requestInfo;
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

        public static class KnownTypes {
            public const string Error = "error";
            public const string NotFound = "404";
            public const string Log = "log";
            public const string FeatureUsage = "usage";
            public const string SessionStart = "start";
            public const string SessionEnd = "end";
        }

        public static class KnownTags {
            public const string Critical = "Critical";
        }

        public static class KnownDataKeys {
            public const string Error = "err";
            public const string SimpleError = "serr";
            public const string RequestInfo = "req";
            public const string TraceLog = "trace";
            //public const string ExceptionInfo = "__ExceptionInfo";
        }
    }
}