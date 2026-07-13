using Exceptionless.Core.Models;

namespace Exceptionless.Core.Models.Ingestion;

public sealed record StackRoute(string StackId, StackStatus Status)
{
    public bool IsDiscarded => Status is StackStatus.Discarded;
}

public sealed record StackRouteCacheEntry(bool Exists, string? StackId = null, StackStatus Status = StackStatus.Open)
{
    public StackRoute? ToRoute() => Exists && StackId is not null ? new StackRoute(StackId, Status) : null;
}
