using System;

namespace Exceptionless.Core.AppStats {
    public static class MetricNames {
        public const string EventsSubmitted = "events.submitted";
        public const string EventsProcessed = "events.processed";
        public const string EventsProcessingTime = "events.processingtime";
        public const string EventsPaidProcessed = "events.paid.processed";
        public const string EventsProcessErrors = "events.processing.errors";
        public const string EventsProcessCancelled = "events.processing.cancelled";
        public const string EventsBotThrottleTriggered = "events.bot-throttle.triggered";

        public const string EventsUserDescriptionSubmitted = "events.description.submitted";
        public const string EventsUserDescriptionQueued = "events.description.queued";
        public const string EventsUserDescriptionDequeued = "events.description.dequeued";
        public const string EventsUserDescriptionProcessed = "events.description.processed";
        public const string EventsUserDescriptionErrors = "events.description.errors";
        public const string EventsUserDescriptionQueueSize = "events.description.queuesize";

        public const string PostsSubmitted = "posts.submitted";
        public const string PostsQueued = "posts.queued";
        public const string PostsQueuedErrors = "posts.queued.errors";
        public const string PostsDequeued = "posts.dequeued";
        public const string PostsParsed = "posts.parsed";
        public const string PostsEventCount = "posts.eventcount";
        public const string PostsSize = "posts.size";
        public const string PostsParseErrors = "posts.parse.errors";
        public const string PostsParsingTime = "posts.parsingtime";
        public const string PostsDiscarded = "posts.discarded";
        public const string PostsBlocked = "posts.blocked";
        public const string PostsQueueSize = "posts.queuesize";

        public const string EmailsQueued = "emails.queued";
        public const string EmailsDequeued = "emails.dequeued";
        public const string EmailsSent = "emails.sent";
        public const string EmailsSendErrors = "emails.send.errors";
        public const string EmailsQueueSize = "emails.queuesize";

        public const string EventNotificationQueueSize = "notification.queuesize";
        public const string WebHookQueueSize = "webhook.queuesize";
    }
}