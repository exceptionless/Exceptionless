using System;
using System.Threading.Tasks;

namespace Exceptionless.Utility {
    public class TaskHelper {
        public static Task<T> FromResult<T>(T value) {
            var taskCompletionSource = new TaskCompletionSource<T>();
            taskCompletionSource.SetResult(value);
            return taskCompletionSource.Task;
        }

        public static Task<T> FromException<T>(Exception e) {
            var taskCompletionSource = new TaskCompletionSource<T>();
            taskCompletionSource.SetException(e);
            return taskCompletionSource.Task;
        }

        public static Task<T> Canceled<T>() {
            var taskCompletionSource = new TaskCompletionSource<T>();
            taskCompletionSource.SetCanceled();
            return taskCompletionSource.Task;
        }
    }
}