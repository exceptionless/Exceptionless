using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Exceptionless.Core.Extensions {
    public static class AsyncExtensions {
        public async static Task<bool> WaitAsync(this AsyncAutoResetEvent autoResetEvent, TimeSpan timeout) {
            var cancelationTokenSource = new CancellationTokenSource(timeout);
            var task = autoResetEvent.WaitAsync(cancelationTokenSource.Token);
            var success = await Task.WhenAny(task, Task.Delay(timeout)) == task;
            Trace.WriteLine("WaitAsync Status: " + task.Status);
            Trace.WriteLine("WaitAsync Success: " + success);
            if (!success)
                cancelationTokenSource.Cancel();

            return success && !task.IsCanceled;
        }

        public static Task TimeoutAfter(this Task task, TimeSpan timeout) {
            if (task.IsCompleted || (timeout == Timeout.InfiniteTimeSpan))
                return task;

            var tcs = new TaskCompletionSource<VoidTypeStruct>();

            if (timeout == TimeSpan.Zero) {
                tcs.SetException(new TimeoutException());
                return tcs.Task;
            }

            var timer = new Timer(state => {
                var myTcs = (TaskCompletionSource<VoidTypeStruct>)state;
                myTcs.TrySetException(new TimeoutException());
            }, tcs, timeout, Timeout.InfiniteTimeSpan);

            task.ContinueWith((antecedent, state) => {
                var tuple = (Tuple<Timer, TaskCompletionSource<VoidTypeStruct>>)state;
                tuple.Item1.Dispose();

                MarshalTaskResults(antecedent, tuple.Item2);
            },
            Tuple.Create(timer, tcs), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            return tcs.Task;
        }

        private struct VoidTypeStruct { }

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
    }
}
