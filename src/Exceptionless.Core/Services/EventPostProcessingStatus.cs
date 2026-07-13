namespace Exceptionless.Core.Services;

public sealed record EventPostProcessingStatus(
    string ProjectId,
    bool IsCompleted,
    DateTimeOffset UpdatedUtc);
