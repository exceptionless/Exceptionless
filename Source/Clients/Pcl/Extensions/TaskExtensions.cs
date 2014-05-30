#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Threading.Tasks;

namespace Exceptionless.Extensions {
    public static class Tasker {
        public static Task Success<TResult>(this Task<TResult> task, Action<Task<TResult>> successor) {
            return task.ContinueWith(_ => {
                if (task.IsCanceled || task.IsFaulted)
                    return task;

                return TaskEx.Run(() => successor(task));
            }).Unwrap();
        }

        public static Task<TResult> Success<TResult>(this Task task, Func<Task, TResult> successor) {
            return task.ContinueWith(_ => {
                if (task.IsFaulted)
                    return FromException<TResult>(task.Exception);

                return task.IsCanceled ? Canceled<TResult>() : TaskEx.Run(() => successor(task));
            }).Unwrap();
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