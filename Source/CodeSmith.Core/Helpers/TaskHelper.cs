#if !PFX_LEGACY_3_5
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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