#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.Core.AppStats {
    public static class StatNames {
        public const string EventsSubmitted = "events.submitted";
        public const string EventsProcessed = "events.processed";
        public const string EventsProcessingTime = "events.processingtime";
        public const string EventsPaidProcessed = "events.paid.processed";
        public const string EventsProcessErrors = "events.processing.errors";
        public const string EventsProcessCancelled = "events.processing.cancelled";
        public const string EventsBlocked = "events.blocked";

        public const string EventsUserDescriptionSubmitted = "events.description.submitted";
        public const string EventsUserDescriptionQueued = "events.description.queued";
        public const string EventsUserDescriptionDequeued = "events.description.dequeued";
        public const string EventsUserDescriptionProcessed = "events.description.processed";
        public const string EventsUserDescriptionErrors = "events.description.errors";

        public const string PostsSubmitted = "posts.submitted";
        public const string PostsQueued = "posts.queued";
        public const string PostsDequeued = "posts.dequeued";
        public const string PostsParsed = "posts.parsed";
        public const string PostsBatchSize = "posts.batchsize";
        public const string PostsParseErrors = "posts.parse.errors";
        public const string PostsParsingTime = "posts.parsingtime";

        public const string EmailsQueued = "emails.queued";
        public const string EmailsDequeued = "emails.dequeued";
        public const string EmailsSent = "emails.sent";
        public const string EmailsSendErrors = "emails.send.errors";
    }
}