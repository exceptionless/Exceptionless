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
    {
        public Event() {
            Tags = new TagSet();
            Data = new DataDictionary();
        }

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
        /// An optional identifier to be used for referencing this event instance at a later time.
        /// </summary>
        public string ReferenceId { get; set; }

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

        public void SetError(object error) {
            Type = KnownTypes.Error;
            Data[KnownDataKeys.Error] = error;
        }

        public void SetRequestInfo(RequestInfo requestInfo) {
            Data[KnownDataKeys.RequestInfo] = requestInfo;
        }

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
            public const string EnvironmentInfo = "env";
            //public const string ExceptionInfo = "__ExceptionInfo";
        }
    }
}