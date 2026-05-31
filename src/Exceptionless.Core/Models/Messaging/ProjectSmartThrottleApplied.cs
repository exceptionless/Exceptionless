namespace Exceptionless.Core.Messaging.Models;

public record ProjectSmartThrottleApplied
{
    public required string OrganizationId { get; init; }
    public required string ProjectId { get; init; }
    public required double SampleRate { get; init; }
    public required int CurrentEventCount { get; init; }
    public required int EventLimit { get; init; }
}
