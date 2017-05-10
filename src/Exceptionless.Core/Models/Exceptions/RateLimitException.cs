using System;

namespace Exceptionless.Core.Models.Exceptions {
    public class RateLimitException : Exception {
        public DateTime RetryAfter { get; set; }
    }
}
