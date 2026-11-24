namespace Exceptionless.Core.Configuration;

public class BulkOperationOptions
{
    /// <summary>How many ids are resolved and executed per batch.</summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>How many temporary errors in a row (without a batch completing in between) end the task.</summary>
    public int MaxTransientFailures { get; set; } = 3;

    /// <summary>How long to wait before continuing after a temporary error. Multiplied by the number of errors in a row.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}
