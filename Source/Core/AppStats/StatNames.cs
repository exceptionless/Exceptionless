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
        public const string ErrorsSubmitted = "errors.submitted";
        public const string ErrorsQueued = "errors.queued";
        public const string ErrorsDequeued = "errors.dequeued";
        public const string ErrorsProcessed = "errors.processed";
        public const string ErrorsProcessingTime = "errors.processingtime";
        public const string ErrorsPaidProcessed = "errors.paid.processed";
        public const string ErrorsProcessingFailed = "errors.processing.failed";
        public const string ErrorsProcessingCancelled = "errors.processing.cancelled";
        public const string ErrorsBotThrottleTriggered = "errors.bot-throttle.triggered";
        public const string ErrorsBlocked = "errors.blocked";
        public const string ErrorsSize = "errors.size";
        public const string ErrorsDiscarded = "errors.discarded";
    }
}