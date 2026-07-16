namespace Exceptionless.Core.Models.Ingestion;

public static class EventIngestionV3Limits
{
    public const int MaximumEventIdLength = 100;
    public const int MaximumTypeLength = 100;
    public const int MaximumSourceLength = 2000;
    public const int MaximumMessageLength = 2000;
    public const int MinimumReferenceIdLength = 8;
    public const int MaximumReferenceIdLength = 100;
    public const int MaximumExceptionTypeLength = 2000;
    public const int MaximumStackTraceLength = 256 * 1024;
    public const int MaximumUserIdentityLength = 255;
    public const int MaximumUserNameLength = 255;
    public const int MaximumVersionLength = 255;
    public const int MaximumLevelLength = 32;
    public const int MaximumClientNameLength = 255;
    public const int MaximumClientVersionLength = 255;
    public const int MaximumStackTitleLength = 1000;
    public const int MaximumTags = 50;
    public const int MaximumTagLength = 100;
    public const int MaximumMetadataEntries = 100;
    public const int MaximumMetadataKeyLength = 255;
    public const int MaximumMetadataValueLength = 16 * 1024;
    public const int MaximumDataTokens = 2000;
    public const int MaximumDataStringLength = 64 * 1024;
    public const int MaximumJsonDepth = 32;
}
