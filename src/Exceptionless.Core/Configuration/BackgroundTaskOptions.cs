namespace Exceptionless.Core.Configuration;

public class BackgroundTaskOptions
{
    /// <summary>How long one attempt to lock a task waits. A busy lock is retried by the resilience policy.</summary>
    public TimeSpan LockAcquireTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
