namespace Exceptionless.Core.Models;

public record UsageInfo {
    public DateTime Date { get; set; }
    public int Limit { get; set; }

    public int Total { get; set; }
    public int Blocked { get; set; }
    public int Discarded { get; set; }
    public int TooBig { get; set; }
}

public record UsageHourInfo {
    public DateTime Date { get; set; }
    public int Total { get; set; }
    public int Blocked { get; set; }
    public int Discarded { get; set; }
    public int TooBig { get; set; }
}

public record UsageInfoResponse {
    public bool IsThrottled { get; set; }
    public UsageInfo CurrentUsage { get; set; }
    public UsageHourInfo CurrentHourUsage { get; set; }
}