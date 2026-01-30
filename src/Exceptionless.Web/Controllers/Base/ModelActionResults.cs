namespace Exceptionless.Web.Controllers;

public record ModelActionResults : WorkInProgressResult
{
    public List<string> Success { get; init; } = new();
    public List<PermissionResult> Failure { get; init; } = new();

    public void AddNotFound(IEnumerable<string> ids)
    {
        Failure.AddRange(ids.Select(PermissionResult.DenyWithNotFound));
    }
}
