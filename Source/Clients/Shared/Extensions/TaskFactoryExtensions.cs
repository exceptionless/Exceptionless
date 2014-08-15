using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Exceptionless.Threading.Tasks {
    public static class TaskFactoryExtensions {
        public static Task FromException(this TaskFactory factory, Exception exception) {
            var tcs = new TaskCompletionSource<object>(factory.CreationOptions);
            tcs.SetException(exception);
            return tcs.Task;
        }

        public static Task<TResult> FromException<TResult>(this TaskFactory factory, Exception exception) {
            var tcs = new TaskCompletionSource<TResult>(factory.CreationOptions);
            tcs.SetException(exception);
            return tcs.Task;
        }

        public static Task<TResult> FromResult<TResult>(this TaskFactory factory, TResult result) {
            var tcs = new TaskCompletionSource<TResult>(factory.CreationOptions);
            tcs.SetResult(result);
            return tcs.Task;
        }

        public static Task WhenAll(this TaskFactory factory, IEnumerable<Task> tasks) {
            return Task.Factory.ContinueWhenAll(tasks.ToArray(), innerTasks => {
                var errors = innerTasks.Where(t => t.IsFaulted || t.IsCanceled).Select(t => t.Exception).ToList();
                if (errors.Count > 0)
                    return Task.Factory.FromException(new AggregateException(errors));

                return Task.Factory.FromResult(0);
            });
        }

        public static Task FromCancellation(this TaskFactory factory, CancellationToken cancellationToken) {
            if (!cancellationToken.IsCancellationRequested)
                throw new ArgumentOutOfRangeException("cancellationToken");

            return new Task(() => { }, cancellationToken);
        }

        public static Task<TResult> FromCancellation<TResult>(this TaskFactory factory, CancellationToken cancellationToken) {
            if (!cancellationToken.IsCancellationRequested)
                throw new ArgumentOutOfRangeException("cancellationToken");

            return new Task<TResult>(DelegateCache<TResult>.DefaultResult, cancellationToken);
        }

        private class DelegateCache<TResult> {
            internal static readonly Func<TResult> DefaultResult = () => default(TResult);
        }

        public static Task<TResult> FromException<TResult>(this TaskFactory<TResult> factory, Exception exception) {
            var tcs = new TaskCompletionSource<TResult>(factory.CreationOptions);
            tcs.SetException(exception);
            return tcs.Task;
        }

        public static Task<TResult> FromResult<TResult>(this TaskFactory<TResult> factory, TResult result) {
            var tcs = new TaskCompletionSource<TResult>(factory.CreationOptions);
            tcs.SetResult(result);
            return tcs.Task;
        }

        public static Task<TResult> FromCancellation<TResult>(this TaskFactory<TResult> factory, CancellationToken cancellationToken) {
            if (!cancellationToken.IsCancellationRequested)
                throw new ArgumentOutOfRangeException("cancellationToken");

            return new Task<TResult>(DelegateCache<TResult>.DefaultResult, cancellationToken);
        }
    }
}