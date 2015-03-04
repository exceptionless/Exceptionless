using System;

namespace Exceptionless.Core.Models {
    public class Event : IData {
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
        /// The geo coordinates where the event happened.
        /// </summary>
        public string Geo { get; set; }

        /// <summary>
        /// The value of the event if any.
        /// </summary>
        public decimal? Value { get; set; }

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
            public const string Internal = "Internal";
        }

        public static class KnownDataKeys {
            public const string Error = "@error";
            public const string SimpleError = "@simple_error";
            public const string RequestInfo = "@request";
            public const string TraceLog = "@trace";
            public const string EnvironmentInfo = "@environment";
            public const string UserInfo = "@user";
            public const string UserDescription = "@user_description";
            public const string Version = "@version";
            public const string Level = "@level";
            public const string SubmissionMethod = "@submission_method";
        }
    }
}