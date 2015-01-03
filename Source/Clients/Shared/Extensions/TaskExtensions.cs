using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Exceptionless.Threading.Tasks {
    public static class TaskExtensions {
        public static Task Then(this Task task, Action<Task> next) {
            if (task == null) throw new ArgumentNullException("task");
            if (next == null) throw new ArgumentNullException("next");

            var tcs = new TaskCompletionSource<AsyncVoid>();

            task.ContinueWith(previousTask => {
                if (previousTask.IsFaulted) tcs.TrySetException(previousTask.Exception);
                else if (previousTask.IsCanceled) tcs.TrySetCanceled();
                else {
                    try {
                        next(previousTask);
                        tcs.TrySetResult(default(AsyncVoid));
                    } catch (Exception ex) {
                        tcs.TrySetException(ex);
                    }
                }
            });

            return tcs.Task;
        }

        public static Task Then(this Task task, Func<Task, Task> next) {
            if (task == null) throw new ArgumentNullException("task");
            if (next == null) throw new ArgumentNullException("next");

            var tcs = new TaskCompletionSource<AsyncVoid>();

            task.ContinueWith(previousTask => {
                if (previousTask.IsFaulted) tcs.TrySetException(previousTask.Exception);
                else if (previousTask.IsCanceled) tcs.TrySetCanceled();
                else {
                    try {
                        next(previousTask).ContinueWith(nextTask => {
                            if (nextTask.IsFaulted) tcs.TrySetException(nextTask.Exception);
                            else if (nextTask.IsCanceled) tcs.TrySetCanceled();
                            else tcs.TrySetResult(default(AsyncVoid));
                        });
                    } catch (Exception ex) {
                        tcs.TrySetException(ex);
                    }
                }
            });

            return tcs.Task;
        }

        public static Task<TNextResult> Then<TNextResult>(this Task task, Func<Task, TNextResult> next) {
            if (task == null) throw new ArgumentNullException("task");
            if (next == null) throw new ArgumentNullException("next");

            var tcs = new TaskCompletionSource<TNextResult>();

            task.ContinueWith(previousTask => {
                if (previousTask.IsFaulted) tcs.TrySetException(previousTask.Exception);
                else if (previousTask.IsCanceled) tcs.TrySetCanceled();
                else {
                    try {
                        tcs.TrySetResult(next(previousTask));
                    } catch (Exception ex) {
                        tcs.TrySetException(ex);
                    }
                }
            });

            return tcs.Task;
        }

        public static Task<TNextResult> Then<TNextResult>(this Task task, Func<Task, Task<TNextResult>> next) {
            if (task == null) throw new ArgumentNullException("task");
            if (next == null) throw new ArgumentNullException("next");

            var tcs = new TaskCompletionSource<TNextResult>();

            task.ContinueWith(previousTask => {
                if (previousTask.IsFaulted) tcs.TrySetException(previousTask.Exception);
                else if (previousTask.IsCanceled) tcs.TrySetCanceled();
                else {
                    try {
                        next(previousTask).ContinueWith(nextTask => {
                            if (nextTask.IsFaulted) tcs.TrySetException(nextTask.Exception);
                            else if (nextTask.IsCanceled) tcs.TrySetCanceled();
                            else {
                                try {
                                    tcs.TrySetResult(nextTask.Result);
                                } catch (Exception ex) {
                                    tcs.TrySetException(ex);
                                }
                            }
                        });
                    } catch (Exception ex) {
                        tcs.TrySetException(ex);
                    }
                }
            });

            return tcs.Task;
        }

        public static Task Then<TResult>(this Task<TResult> task, Action<Task<TResult>> next) {
            if (task == null) throw new ArgumentNullException("task");
            if (next == null) throw new ArgumentNullException("next");

            var tcs = new TaskCompletionSource<AsyncVoid>();

            task.ContinueWith(previousTask => {
                if (previousTask.IsFaulted) tcs.TrySetException(previousTask.Exception);
                else if (previousTask.IsCanceled) tcs.TrySetCanceled();
                else {
                    try {
                        next(previousTask);
                        tcs.TrySetResult(default(AsyncVoid));
                    } catch (Exception ex) {
                        tcs.TrySetException(ex);
                    }
                }
            });

            return tcs.Task;
        }

        public static Task Then<TResult>(this Task<TResult> task, Func<Task<TResult>, Task> next) {
            if (task == null) throw new ArgumentNullException("task");
            if (next == null) throw new ArgumentNullException("next");

            var tcs = new TaskCompletionSource<AsyncVoid>();

            task.ContinueWith(previousTask => {
                if (previousTask.IsFaulted) tcs.TrySetException(previousTask.Exception);
                else if (previousTask.IsCanceled) tcs.TrySetCanceled();
                else {
                    try {
                        next(previousTask).ContinueWith(nextTask => {
                            if (nextTask.IsFaulted) tcs.TrySetException(nextTask.Exception);
                            else if (nextTask.IsCanceled) tcs.TrySetCanceled();
                            else tcs.TrySetResult(default(AsyncVoid));
                        });
                    } catch (Exception ex) {
                        tcs.TrySetException(ex);
                    }
                }
            });

            return tcs.Task;
        }

        public static Task<TNextResult> Then<TResult, TNextResult>(this Task<TResult> task, Func<Task<TResult>, TNextResult> next) {
            if (task == null) throw new ArgumentNullException("task");
            if (next == null) throw new ArgumentNullException("next");

            var tcs = new TaskCompletionSource<TNextResult>();

            task.ContinueWith(previousTask => {
                if (previousTask.IsFaulted) tcs.TrySetException(previousTask.Exception);
                else if (previousTask.IsCanceled) tcs.TrySetCanceled();
                else {
                    try {
                        tcs.TrySetResult(next(previousTask));
                    } catch (Exception ex) {
                        tcs.TrySetException(ex);
                    }
                }
            });

            return tcs.Task;
        }

        public static Task<TNextResult> Then<TResult, TNextResult>(this Task<TResult> task, Func<Task<TResult>, Task<TNextResult>> next) {
            if (task == null) throw new ArgumentNullException("task");
            if (next == null) throw new ArgumentNullException("next");

            var tcs = new TaskCompletionSource<TNextResult>();

            task.ContinueWith(previousTask => {
                if (previousTask.IsFaulted) tcs.TrySetException(previousTask.Exception);
                else if (previousTask.IsCanceled) tcs.TrySetCanceled();
                else {
                    try {
                        next(previousTask).ContinueWith(nextTask => {
                            if (nextTask.IsFaulted) tcs.TrySetException(nextTask.Exception);
                            else if (nextTask.IsCanceled) tcs.TrySetCanceled();
                            else {
                                try {
                                    tcs.TrySetResult(nextTask.Result);
                                } catch (Exception ex) {
                                    tcs.TrySetException(ex);
                                }
                            }
                        });
                    } catch (Exception ex) {
                        tcs.TrySetException(ex);
                    }
                }
            });

            return tcs.Task;
        }

        public static void Finally(this Task task, Action<Exception> exceptionHandler, Action finalAction = null) {
            task.ContinueWith(t => {
                if (finalAction != null) finalAction();

                if (t.IsCanceled || !t.IsFaulted || exceptionHandler == null) return;
                var innerException = t.Exception.Flatten().InnerExceptions.FirstOrDefault();
                exceptionHandler(innerException ?? t.Exception);
            });
        }

        public static Task<T> FromException<T>(Exception e) {
            var taskCompletionSource = new TaskCompletionSource<T>();
            taskCompletionSource.SetException(e);
            return taskCompletionSource.Task;
        }

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