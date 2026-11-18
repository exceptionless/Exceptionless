namespace Exceptionless.Core.Configuration;

public class BulkOperationOptions
{
    public int BatchSize { get; set; } = 500;
    public int MaxTransientFailures { get; set; } = 3;

    /// <summary>Multiplied by the number of consecutive failures before retrying a batch.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}
