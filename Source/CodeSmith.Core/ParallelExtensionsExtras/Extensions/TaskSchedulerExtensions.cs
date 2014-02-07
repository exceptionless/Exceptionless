//--------------------------------------------------------------------------
// 
//  Copyright (c) Microsoft Corporation.  All rights reserved. 
// 
//  File: TaskSchedulerExtensions.cs
//
//--------------------------------------------------------------------------

#if !SILVERLIGHT && !PFX_LEGACY_3_5

namespace System.Threading.Tasks
{
    /// <summary>Extension methods for TaskScheduler.</summary>
    public static class TaskSchedulerExtensions
    {
        /// <summary>Gets a SynchronizationContext that targets this TaskScheduler.</summary>
        /// <param name="scheduler">The target scheduler.</param>
        /// <returns>A SynchronizationContext that targets this scheduler.</returns>
        public static SynchronizationContext ToSynchronizationContext(this TaskScheduler scheduler)
        {
            return new TaskSchedulerSynchronizationContext(scheduler);
        }

        /// <summary>Provides a SynchronizationContext wrapper for a TaskScheduler.</summary>
        private sealed class TaskSchedulerSynchronizationContext : SynchronizationContext
        {
            /// <summary>The scheduler.</summary>
            private TaskScheduler _scheduler;

            /// <summary>Initializes the context with the specified scheduler.</summary>
            /// <param name="scheduler">The scheduler to target.</param>
            internal TaskSchedulerSynchronizationContext(TaskScheduler scheduler)
            {
                if (scheduler == null) throw new ArgumentNullException("scheduler");
                _scheduler = scheduler;
            }

            /// <summary>Dispatches an asynchronous message to the synchronization context.</summary>
            /// <param name="d">The System.Threading.SendOrPostCallback delegate to call.</param>
            /// <param name="state">The object passed to the delegate.</param>
            public override void Post(SendOrPostCallback d, object state)
            {
                Task.Factory.StartNew(() => d(state), CancellationToken.None, TaskCreationOptions.None, _scheduler);
            }

            /// <summary>Dispatches a synchronous message to the synchronization context.</summary>
            /// <param name="d">The System.Threading.SendOrPostCallback delegate to call.</param>
            /// <param name="state">The object passed to the delegate.</param>
            public override void Send(SendOrPostCallback d, object state)
            {
                Task t = new Task(() => d(state));
                t.RunSynchronously(_scheduler);
                t.Wait();
            }
        }
    }
}

#endif