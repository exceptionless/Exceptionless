namespace Exceptionless.Core.Models;

public record UsageInfo
{
    public DateTime Date { get; init; }
    public int Limit { get; set; }

    public int Total { get; set; }
    public int Blocked { get; set; }
    public int Discarded { get; set; }
    public int TooBig { get; set; }
}

public record UsageHourInfo
{
    public DateTime Date { get; init; }
    public int Total { get; set; }
    public int Blocked { get; set; }
    public int Discarded { get; set; }
    public int TooBig { get; set; }
}

public record UsageInfoResponse
{
    public required bool IsThrottled { get; init; }
    public required UsageInfo CurrentUsage { get; init; }
    public required UsageHourInfo CurrentHourUsage { get; init; }
}
