namespace Exceptionless.Core.Models;

public record AssistantUsageInfo
{
    public DateTime Date { get; init; }
    public string? PlanId { get; set; }
    public long Turns { get; set; }
    public long Completed { get; set; }
    public long Failed { get; set; }
    public long Cancelled { get; set; }
    public long ProviderRequests { get; set; }
    public long ToolCalls { get; set; }
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long CostInMicrodollars { get; set; }
    public long BlockedByConcurrency { get; set; }
    public long BlockedByRateLimit { get; set; }
    public long BlockedByTokenLimit { get; set; }
    public long BlockedByCostLimit { get; set; }
    public DateTime LastUsedUtc { get; set; }
}

public sealed record AssistantUsageIncrement
{
    public long Turns { get; init; }
    public long Completed { get; init; }
    public long Failed { get; init; }
    public long Cancelled { get; init; }
    public long ProviderRequests { get; init; }
    public long ToolCalls { get; init; }
    public long PromptTokens { get; init; }
    public long CompletionTokens { get; init; }
    public long CostInMicrodollars { get; init; }
    public long BlockedByConcurrency { get; init; }
    public long BlockedByRateLimit { get; init; }
    public long BlockedByTokenLimit { get; init; }
    public long BlockedByCostLimit { get; init; }

    public bool HasValue => Turns > 0
        || Completed > 0
        || Failed > 0
        || Cancelled > 0
        || ProviderRequests > 0
        || ToolCalls > 0
        || PromptTokens > 0
        || CompletionTokens > 0
        || CostInMicrodollars > 0
        || BlockedByConcurrency > 0
        || BlockedByRateLimit > 0
        || BlockedByTokenLimit > 0
        || BlockedByCostLimit > 0;
}
