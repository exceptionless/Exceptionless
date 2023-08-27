namespace Exceptionless.Core.Models;

public record PreviousAndNextEventIdResult
{
    public string? Previous { get; init; }
    public string? Next { get; init; }
}
