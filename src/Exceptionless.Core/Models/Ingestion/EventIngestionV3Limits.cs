namespace Exceptionless.Core.Models.Ingestion;

public static class EventIngestionV3Limits
{
    public const int MaximumEventIdLength = 128;
    public const int MaximumTypeLength = 100;
    public const int MaximumSourceLength = 2000;
    public const int MaximumMessageLength = 4000;
    public const int MaximumReferenceIdLength = 100;
    public const int MaximumExceptionTypeLength = 2000;
    public const int MaximumStackTraceLength = 256 * 1024;
    public const int MaximumUserIdentityLength = 255;
    public const int MaximumUserNameLength = 255;
    public const int MaximumTags = 50;
    public const int MaximumTagLength = 255;
    public const int MaximumJsonDepth = 32;
}
