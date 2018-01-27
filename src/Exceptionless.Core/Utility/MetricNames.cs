using System;

namespace Exceptionless.Core.AppStats {
    public static class MetricNames {
        public const string EventsSubmitted = "events.submitted";
        public const string EventsProcessed = "events.processed";
        public const string EventsProcessingTime = "events.processingtime";
        public const string EventsPaidProcessed = "events.paid.processed";
        public const string EventsProcessErrors = "events.processing.errors";
        public const string EventsProcessCancelled = "events.processing.cancelled";
        public const string EventsRetryCount = "events.retry.count";
        public const string EventsRetryErrors = "events.retry.errors";
        public const string EventsFieldCount = "events.field.count";

        public const string PostsParsed = "posts.parsed";
        public const string PostsEventCount = "posts.eventcount";
        public const string PostsSize = "posts.size";
        public const string PostsParseErrors = "posts.parse.errors";
        public const string PostsFileInfoTime = "posts.fileinfotime";
        public const string PostsMarkFileActiveTime = "posts.markfileactivetime";
        public const string PostsUpdateEventLimitTime = "posts.updateeventlimitime";
        public const string PostsParsingTime = "posts.parsingtime";
        public const string PostsRetryTime = "posts.retrytime";
        public const string PostsAbandonTime = "posts.abandontime";
        public const string PostsCompleteTime = "posts.completetime";
        public const string PostsDiscarded = "posts.discarded";
        public const string PostsBlocked = "posts.blocked";

        public const string PostsMessageSize = "posts.message.size";
        public const string PostsCompressedSize = "posts.compressed.size";
        public const string PostsUncompressedSize = "posts.uncompressed.size";
        public const string PostsDecompressionTime = "posts.decompression.time";
        public const string PostsDecompressionErrors = "posts.decompression.errors";

        public const string UsageGeocodingApi = "usage.geocoding";
        public const string ThrottleLimitExceeded = "throttle.limitexceeded";
    }
}