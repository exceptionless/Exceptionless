namespace Exceptionless.Web.Controllers;

public record ModelActionResults : WorkInProgressResult
{
    public List<string> Success { get; } = new();
    public List<PermissionResult> Failure { get; } = new();

    public void AddNotFound(IEnumerable<string> ids)
    {
        Failure.AddRange(ids.Select(PermissionResult.DenyWithNotFound));
    }
}
