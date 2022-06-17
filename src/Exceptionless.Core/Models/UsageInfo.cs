namespace Exceptionless.Core.Models;

public record UsageInfo {
    public DateTime Date { get; set; }
    public int Limit { get; set; }

    public int Total { get; set; }
    public int Blocked { get; set; }
    public int TooBig { get; set; }
}

public record OverageInfo {
    public DateTime Date { get; set; }
    public int Total { get; set; }
    public int Blocked { get; set; }
    public int TooBig { get; set; }
}

public record UsageInfoResponse : UsageInfo {
    public bool IsThrottled { get; set; }
    public OverageInfo Overage { get; set; }
}
