using System;

#if !EMBEDDED
namespace Exceptionless.Api.Utility {
#else
namespace Exceptionless.Submission.Net {
#endif
    public static class ExceptionlessHeaders {
        public const string Bearer = "Bearer";
        public const string LegacyConfigurationVersion = "v";
        public const string ConfigurationVersion = "X-Exceptionless-ConfigVersion";
        public const string Client = "X-Exceptionless-Client";
        public const string RateLimit = "X-RateLimit-Limit";
        public const string RateLimitRemaining = "X-RateLimit-Remaining";
        public const string LimitedByPlan = "X-LimitedByPlan";
    }
}