using System.Threading;
#if !PFX_LEGACY_3_5
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Diagnostics;

#if !EMBEDDED
namespace CodeSmith.Core.Component {
    public
#else
namespace Exceptionless.Utility {
    internal
#endif
    static class TaskHelper {
        private static readonly Task _defaultCompleted = FromResult(new AsyncVoid());
        private static readonly Task<object> _completedTaskReturningNull = FromResult<object>(null);

        public static Task Canceled() {
            return CancelCache<AsyncVoid>.Canceled;
        }

        public static Task<TResult> Canceled<TResult>() {
            return CancelCache<TResult>.Canceled;
        }

        public static Task Completed() {
            return _defaultCompleted;
        }

        public static Task FromError(Exception exception) {
            return FromError<AsyncVoid>(exception);
        }

        public static Task<TResult> FromError<TResult>(Exception exception) {
            var completionSource = new TaskCompletionSource<TResult>();
            completionSource.SetException(exception);
            return completionSource.Task;
        }

        public static Task FromErrors(IEnumerable<Exception> exceptions) {
            return FromErrors<AsyncVoid>(exceptions);
        }

        public static Task<TResult> FromErrors<TResult>(IEnumerable<Exception> exceptions) {
            var completionSource = new TaskCompletionSource<TResult>();
            completionSource.SetException(exceptions);
            return completionSource.Task;
        }

        public static Task<TResult> FromResult<TResult>(TResult result) {
            var completionSource = new TaskCompletionSource<TResult>();
            completionSource.SetResult(result);
            return completionSource.Task;
        }

        public static Task<object> NullResult() {
            return _completedTaskReturningNull;
        }

        public static bool SetIfTaskFailed<TResult>(this TaskCompletionSource<TResult> tcs, Task source) {
            switch (source.Status) {
                case TaskStatus.Canceled:
                case TaskStatus.Faulted:
                    return TrySetFromTask(tcs, source);
                default:
                    return false;
            }
        }

        public static bool TrySetFromTask<TResult>(this TaskCompletionSource<TResult> tcs, Task source) {
            if (source.Status == TaskStatus.Canceled)
                return tcs.TrySetCanceled();
            if (source.Status == TaskStatus.Faulted)
                return tcs.TrySetException(source.Exception.InnerExceptions);
            if (source.Status != TaskStatus.RanToCompletion)
                return false;
            var task = source as Task<TResult>;
            return tcs.TrySetResult(task == null ? default(TResult) : task.Result);
        }

        public static bool TrySetFromTask<TResult>(this TaskCompletionSource<Task<TResult>> tcs, Task source) {
            if (source.Status == TaskStatus.Canceled)
                return tcs.TrySetCanceled();
            if (source.Status == TaskStatus.Faulted)
                return tcs.TrySetException(source.Exception.InnerExceptions);
            if (source.Status != TaskStatus.RanToCompletion)
                return false;
            var task = source as Task<Task<TResult>>;
            if (task != null)
                return tcs.TrySetResult(task.Result);
            var result = source as Task<TResult>;

            return tcs.TrySetResult(result ?? FromResult(default(TResult)));
        }

        public static async Task RunPeriodic(Func<Task> action, TimeSpan period, CancellationToken? cancellationToken = null, TimeSpan? initialDelay = null) {
            if (!cancellationToken.HasValue)
                cancellationToken = CancellationToken.None;

            if (initialDelay.HasValue && initialDelay.Value > TimeSpan.Zero)
                await Task.Delay(initialDelay.Value, cancellationToken.Value);

            while (!cancellationToken.Value.IsCancellationRequested) {
                await Task.Delay(period, cancellationToken.Value);
                try {
                    await action();
                } catch (Exception ex) {
                    Trace.TraceError(ex.Message);
                }
            }
        }

        public async static Task<bool> DelayUntil(Func<bool> condition, TimeSpan? timeout = null, int checkInterval = 100) {
            DateTime start = DateTime.Now;
            while (!condition()) {
                if (timeout.HasValue && DateTime.Now.Subtract(start) > timeout.Value)
                    return false;

                await Task.Delay(TimeSpan.FromMilliseconds(checkInterval));
            }

            return true;
        }

        public static Task TimeoutAfter(this Task task, TimeSpan timeout) {
            if (task.IsCompleted || (timeout == Timeout.InfiniteTimeSpan))
                return task;

            var tcs = new TaskCompletionSource<AsyncVoid>();

            if (timeout == TimeSpan.Zero) {
                tcs.SetException(new TimeoutException());
                return tcs.Task;
            }

            var timer = new Timer(state => {
                var myTcs = (TaskCompletionSource<AsyncVoid>)state;
                myTcs.TrySetException(new TimeoutException());
            }, tcs, timeout, Timeout.InfiniteTimeSpan);

            task.ContinueWith((antecedent, state) => {
                var tuple = (Tuple<Timer, TaskCompletionSource<AsyncVoid>>)state;
                tuple.Item1.Dispose();

                MarshalTaskResults(antecedent, tuple.Item2);
            },
            Tuple.Create(timer, tcs), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            return tcs.Task;
        }

        internal static void MarshalTaskResults<TResult>(
            Task source, TaskCompletionSource<TResult> proxy) {
            switch (source.Status) {
            case TaskStatus.Faulted:
                proxy.TrySetException(source.Exception);
                break;
            case TaskStatus.Canceled:
                proxy.TrySetCanceled();
                break;
            case TaskStatus.RanToCompletion:
                var castedSource = source as Task<TResult>;
                proxy.TrySetResult(castedSource == null ? default(TResult) : castedSource.Result);
                break;
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 1)]
        private struct AsyncVoid {
        }

        private static class CancelCache<TResult> {
            public static readonly Task<TResult> Canceled = GetCancelledTask();

            static CancelCache() {
            }

            private static Task<TResult> GetCancelledTask() {
                var completionSource = new TaskCompletionSource<TResult>();
                completionSource.SetCanceled();
                return completionSource.Task;
            }
        }
    }
}

#endif