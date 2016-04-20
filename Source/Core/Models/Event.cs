using System;
using System.Collections.Generic;

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
        /// The number of duplicated events.
        /// </summary>
        public int? Count { get; set; }

        /// <summary>
        /// Optional data entries that contain additional information about this event.
        /// </summary>
        public DataDictionary Data { get; set; }

        /// <summary>
        /// An optional identifier to be used for referencing this event instance at a later time.
        /// </summary>
        public string ReferenceId { get; set; }

        protected bool Equals(Event other) {
            return string.Equals(Type, other.Type) && string.Equals(Source, other.Source) && Tags.CollectionEquals(other.Tags) && string.Equals(Message, other.Message) && string.Equals(Geo, other.Geo) && Value == other.Value && Equals(Data, other.Data);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((Event)obj);
        }

        private static readonly List<string> _exclusions = new List<string> { KnownDataKeys.TraceLog }; 
        public override int GetHashCode() {
            unchecked {
                var hashCode = Type == null ? 0 : Type.GetHashCode();
                hashCode = (hashCode * 397) ^ (Source == null ? 0 : Source.GetHashCode());
                hashCode = (hashCode * 397) ^ (Tags == null ? 0 : Tags.GetCollectionHashCode());
                hashCode = (hashCode * 397) ^ (Message == null ? 0 : Message.GetHashCode());
                hashCode = (hashCode * 397) ^ (Geo == null ? 0 : Geo.GetHashCode());
                hashCode = (hashCode * 397) ^ Value.GetHashCode();
                hashCode = (hashCode * 397) ^ (Data == null ? 0 : Data.GetCollectionHashCode(_exclusions));
                return hashCode;
            }
        }

        public static class KnownTypes {
            public const string Error = "error";
            public const string FeatureUsage = "usage";
            public const string Log = "log";
            public const string NotFound = "404";
            public const string Session = "session";
            public const string SessionEnd = "sessionend";
            public const string SessionHeartbeat = "heartbeat";
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
            public const string Location = "@location";
            public const string SubmissionMethod = "@submission_method";
            public const string SessionEnd = "sessionend";
            public const string SessionHasError = "haserror";
            public const string ManualStackingInfo = "@stack";
        }
    }
}