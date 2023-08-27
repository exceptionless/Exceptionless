namespace Exceptionless.Web.Controllers;

public class ModelActionResults : WorkInProgressResult
{
    public List<string> Success { get; set; } = new();
    public List<PermissionResult> Failure { get; set; } = new();

    public void AddNotFound(IEnumerable<string> ids)
    {
        Failure.AddRange(ids.Select(PermissionResult.DenyWithNotFound));
    }
}
