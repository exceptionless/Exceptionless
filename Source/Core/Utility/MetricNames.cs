using System;

namespace Exceptionless.Core.AppStats {
    public static class MetricNames {
        public const string EventsSubmitted = "events.submitted";
        public const string EventsProcessed = "events.processed";
        public const string EventsProcessingTime = "events.processingtime";
        public const string EventsPaidProcessed = "events.paid.processed";
        public const string EventsProcessErrors = "events.processing.errors";
        public const string EventsProcessCancelled = "events.processing.cancelled";
        
        public const string PostsParsed = "posts.parsed";
        public const string PostsEventCount = "posts.eventcount";
        public const string PostsSize = "posts.size";
        public const string PostsParseErrors = "posts.parse.errors";
        public const string PostsParsingTime = "posts.parsingtime";
        public const string PostsDiscarded = "posts.discarded";
        public const string PostsBlocked = "posts.blocked";

        public const string UsageGeocodingApi = "usage.geocoding";
    }
}