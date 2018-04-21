using System;

namespace Exceptionless.Api.Utility {
    public static class Headers {
        public const string Bearer = "Bearer";
        public const string LegacyConfigurationVersion = "v";
        public const string ConfigurationVersion = "X-Exceptionless-ConfigVersion";
        public const string Client = "X-Exceptionless-Client";
        public const string RateLimit = "X-RateLimit-Limit";
        public const string RateLimitRemaining = "X-RateLimit-Remaining";
        public const string LimitedByPlan = "X-LimitedByPlan";

        public const string ContentEncoding = "Content-Encoding";
    }
}