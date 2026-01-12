using Foundatio.AsyncEx;

namespace Exceptionless.Tests.Extensions;

public static class TaskExtensions
{
    public static Task WaitAsync(this AsyncCountdownEvent countdownEvent, TimeSpan timeout)
    {
        return countdownEvent.WaitAsync().WaitAsync(timeout);
    }
}
