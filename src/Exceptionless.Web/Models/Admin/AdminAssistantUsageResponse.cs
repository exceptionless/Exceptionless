namespace Exceptionless.Web.Models.Admin;

public sealed record AdminAssistantUsageResponse(
    DateTime Month,
    long ActiveOrganizations,
    long Turns,
    long PromptTokens,
    long CompletionTokens,
    decimal CostUsd,
    IReadOnlyCollection<AdminAssistantOrganizationUsage> Organizations);

public sealed record AdminAssistantOrganizationUsage(
    string OrganizationId,
    string OrganizationName,
    string PlanId,
    DateTime LastUsedUtc,
    long Turns,
    long Completed,
    long Failed,
    long Cancelled,
    long ProviderRequests,
    long ToolCalls,
    long PromptTokens,
    long CompletionTokens,
    decimal CostUsd,
    long BlockedByConcurrency,
    long BlockedByRateLimit,
    long BlockedByTokenLimit,
    long BlockedByCostLimit,
    long? MonthlyTokenLimit,
    decimal? MonthlyCostLimitUsd,
    decimal? TokenUtilization,
    decimal? CostUtilization);
