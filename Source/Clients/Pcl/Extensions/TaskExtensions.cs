#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Threading.Tasks;
using Exceptionless.Utility;

namespace Exceptionless.Extensions {
    public static class TaskExtensions {
        public static Task Success<TResult>(this Task<TResult> task, Action<Task<TResult>> successor) {
            return task.ContinueWith(_ => {
                if (task.IsCanceled || task.IsFaulted)
                    return task;

                return Task.Factory.StartNew(() => successor(task));
            }).Unwrap();
        }

        public static Task<TResult> Success<TResult>(this Task task, Func<Task, TResult> successor) {
            return task.ContinueWith(_ => {
                if (task.IsFaulted)
                    return TaskHelper.FromException<TResult>(task.Exception);

                return task.IsCanceled ? TaskHelper.Canceled<TResult>() : Task.Factory.StartNew(() => successor(task));
            }).Unwrap();
        }
    }
}