namespace Exceptionless.Web.Controllers;

public record WorkInProgressResult
{
    public WorkInProgressResult()
    {
    }

    public WorkInProgressResult(IEnumerable<string> workers) : this()
    {
        Workers.AddRange(workers);
    }

    public List<string> Workers { get; init; } = new();
}
