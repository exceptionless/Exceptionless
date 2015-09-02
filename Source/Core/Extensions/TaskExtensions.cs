using System;
using System.Threading.Tasks;

namespace Exceptionless.Core.Extensions {
    public static class TaskExtensions {
        public static Task IgnoreExceptions(this Task task) {
            task.ContinueWith(c => { var ignored = c.Exception; },
                TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously);
            return task;
        }
    }
}
