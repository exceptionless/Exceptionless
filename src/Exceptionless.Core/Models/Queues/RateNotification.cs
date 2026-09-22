using Foundatio.Queues;

namespace Exceptionless.Core.Queues.Models;

public class RateNotification : IHaveUniqueIdentifier
{
    public required string RuleId { get; set; }
    public required int RuleVersion { get; set; }
    public required string OrganizationId { get; set; }
    public required string ProjectId { get; set; }
    public required string UserId { get; set; }
    public required string SubjectKey { get; set; }
    public string? StackId { get; set; }
    public required DateTime WindowStartUtc { get; set; }
    public required DateTime WindowEndUtc { get; set; }
    public required long ObservedCount { get; set; }
    public required int Threshold { get; set; }

    public string UniqueIdentifier => $"RateNotification:{RuleId}:{RuleVersion}:{WindowEndUtc.Ticks}";
}
