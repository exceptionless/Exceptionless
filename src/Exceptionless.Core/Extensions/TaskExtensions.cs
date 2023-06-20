using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Exceptionless.Core.Extensions;

public static class TaskExtensions
{
    [DebuggerStepThrough]
    public static ConfiguredTaskAwaitable<TResult> AnyContext<TResult>(this Task<TResult> task)
    {
        return task.ConfigureAwait(continueOnCapturedContext: false);
    }

    [DebuggerStepThrough]
    public static ConfiguredTaskAwaitable AnyContext(this Task task)
    {
        return task.ConfigureAwait(continueOnCapturedContext: false);
    }
}
