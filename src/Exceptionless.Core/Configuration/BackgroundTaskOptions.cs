namespace Exceptionless.Core.Configuration;

public class BackgroundTaskOptions
{
    public TimeSpan LockAcquireTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
