namespace Exceptionless.Web.Utility;

public static class Headers
{
    public const string ContentEncoding = "Content-Encoding";
    public const string EventPostId = "X-Exceptionless-Event-Post-Id";
    public const string TrackEventPost = "X-Exceptionless-Track-Event-Post";
    public const string LegacyConfigurationVersion = "v";
    public const string ConfigurationVersion = "X-Exceptionless-ConfigVersion";
    public const string Client = "X-Exceptionless-Client";
    public const string RateLimit = "X-RateLimit-Limit";
    public const string RateLimitRemaining = "X-RateLimit-Remaining";
    public const string ResultCount = "X-Result-Count";
}
