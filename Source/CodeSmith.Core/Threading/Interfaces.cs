// Ami Bar
// amibar@gmail.com

using System;
using System.Threading;

namespace CodeSmith.Core.Threading
{
	#region Delegates

	/// <summary>
	/// A delegate that represents the method to run as the work item
	/// </summary>
	/// <param name="state">A state object for the method to run</param>
	public delegate object WorkItemCallback(object state);

	/// <summary>
	/// A delegate to call after the WorkItemCallback completed
	/// </summary>
	/// <param name="wir">The work item result object</param>
	public delegate void PostExecuteWorkItemCallback(IWorkItemResult wir);

	/// <summary>
	/// A delegate to call when a WorkItemsGroup becomes idle
	/// </summary>
	/// <param name="workItemsGroup">A reference to the WorkItemsGroup that became idle</param>
	public delegate void WorkItemsGroupIdleHandler(IWorkItemsGroup workItemsGroup);

	#endregion

	#region WorkItem Priority

	public enum WorkItemPriority
	{
		Lowest,
		BelowNormal,
		Normal,
		AboveNormal,
		Highest,
	}

	#endregion

	#region IHasWorkItemPriority interface 

	public interface IHasWorkItemPriority
	{
		WorkItemPriority WorkItemPriority { get; }
	}

	#endregion

	#region IWorkItemsGroup interface 

	/// <summary>
	/// IWorkItemsGroup interface
	/// </summary>
	public interface IWorkItemsGroup
	{
		/// <summary>
		/// Get/Set the name of the WorkItemsGroup
		/// </summary>
		string Name { get; set; }

		IWorkItemResult QueueWorkItem(WorkItemCallback callback);
		IWorkItemResult QueueWorkItem(WorkItemCallback callback, WorkItemPriority workItemPriority);
		IWorkItemResult QueueWorkItem(WorkItemCallback callback, object state);
		IWorkItemResult QueueWorkItem(WorkItemCallback callback, object state, WorkItemPriority workItemPriority);
		IWorkItemResult QueueWorkItem(WorkItemCallback callback, object state, PostExecuteWorkItemCallback postExecuteWorkItemCallback);
		IWorkItemResult QueueWorkItem(WorkItemCallback callback, object state, PostExecuteWorkItemCallback postExecuteWorkItemCallback, WorkItemPriority workItemPriority);
		IWorkItemResult QueueWorkItem(WorkItemCallback callback, object state, PostExecuteWorkItemCallback postExecuteWorkItemCallback, CallToPostExecute callToPostExecute);
		IWorkItemResult QueueWorkItem(WorkItemCallback callback, object state, PostExecuteWorkItemCallback postExecuteWorkItemCallback, CallToPostExecute callToPostExecute, WorkItemPriority workItemPriority);

		IWorkItemResult QueueWorkItem(WorkItemInfo workItemInfo, WorkItemCallback callback);
		IWorkItemResult QueueWorkItem(WorkItemInfo workItemInfo, WorkItemCallback callback, object state);

		void WaitForIdle();
		bool WaitForIdle(TimeSpan timeout);
		bool WaitForIdle(int millisecondsTimeout);

		int WaitingCallbacks { get; }
		event WorkItemsGroupIdleHandler OnIdle;

		void Cancel();
		void Start();
	}

	#endregion

	#region CallToPostExecute enumerator

	[Flags]
	public enum CallToPostExecute
	{
		Never                    = 0x00,
		WhenWorkItemCanceled     = 0x01,
		WhenWorkItemNotCanceled  = 0x02,
		Always                   = WhenWorkItemCanceled | WhenWorkItemNotCanceled,
	}

	#endregion

	#region IWorkItemResult interface

	/// <summary>
	/// IWorkItemResult interface
	/// </summary>
	public interface IWorkItemResult
	{
		/// <summary>
		/// Get the result of the work item.
		/// If the work item didn't run yet then the caller waits.
		/// </summary>
		/// <returns>The result of the work item</returns>
		object GetResult();

		/// <summary>
		/// Get the result of the work item.
		/// If the work item didn't run yet then the caller waits until timeout.
		/// </summary>
		/// <returns>The result of the work item</returns>
		/// On timeout throws WorkItemTimeoutException
		object GetResult(
			int millisecondsTimeout,
			bool exitContext);

		/// <summary>
		/// Get the result of the work item.
		/// If the work item didn't run yet then the caller waits until timeout.
		/// </summary>
		/// <returns>The result of the work item</returns>
		/// On timeout throws WorkItemTimeoutException
		object GetResult(			
			TimeSpan timeout,
			bool exitContext);

		/// <summary>
		/// Get the result of the work item.
		/// If the work item didn't run yet then the caller waits until timeout or until the cancelWaitHandle is signaled.
		/// </summary>
		/// <param name="millisecondsTimeout">Timeout in milliseconds, or -1 for infinite</param>
		/// <param name="exitContext">
		/// true to exit the synchronization domain for the context before the wait (if in a synchronized context), and reacquire it; otherwise, false. 
		/// </param>
		/// <param name="cancelWaitHandle">A cancel wait handle to interrupt the blocking if needed</param>
		/// <returns>The result of the work item</returns>
		/// On timeout throws WorkItemTimeoutException
		/// On cancel throws WorkItemCancelException
		object GetResult(			
			int millisecondsTimeout,
			bool exitContext,
			WaitHandle cancelWaitHandle);

		/// <summary>
		/// Get the result of the work item.
		/// If the work item didn't run yet then the caller waits until timeout or until the cancelWaitHandle is signaled.
		/// </summary>
		/// <returns>The result of the work item</returns>
		/// On timeout throws WorkItemTimeoutException
		/// On cancel throws WorkItemCancelException
		object GetResult(			
			TimeSpan timeout,
			bool exitContext,
			WaitHandle cancelWaitHandle);

		/// <summary>
		/// Get the result of the work item.
		/// If the work item didn't run yet then the caller waits.
		/// </summary>
		/// <param name="e">Filled with the exception if one was thrown</param>
		/// <returns>The result of the work item</returns>
		object GetResult(out Exception e);

        /// <summary>
        /// Get the result of the work item.
        /// If the work item didn't run yet then the caller waits until timeout.
        /// </summary>
        /// <param name="millisecondsTimeout">The milliseconds timeout.</param>
        /// <param name="exitContext">if set to <c>true</c> [exit context].</param>
        /// <param name="e">Filled with the exception if one was thrown</param>
        /// <returns>The result of the work item</returns>
        /// On timeout throws WorkItemTimeoutException
		object GetResult(
			int millisecondsTimeout,
			bool exitContext,
			out Exception e);

        /// <summary>
        /// Get the result of the work item.
        /// If the work item didn't run yet then the caller waits until timeout.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <param name="exitContext">if set to <c>true</c> [exit context].</param>
        /// <param name="e">Filled with the exception if one was thrown</param>
        /// <returns>The result of the work item</returns>
        /// On timeout throws WorkItemTimeoutException
		object GetResult(			
			TimeSpan timeout,
			bool exitContext,
			out Exception e);

		/// <summary>
		/// Get the result of the work item.
		/// If the work item didn't run yet then the caller waits until timeout or until the cancelWaitHandle is signaled.
		/// </summary>
		/// <param name="millisecondsTimeout">Timeout in milliseconds, or -1 for infinite</param>
		/// <param name="exitContext">
		/// true to exit the synchronization domain for the context before the wait (if in a synchronized context), and reacquire it; otherwise, false. 
		/// </param>
		/// <param name="cancelWaitHandle">A cancel wait handle to interrupt the blocking if needed</param>
		/// <param name="e">Filled with the exception if one was thrown</param>
		/// <returns>The result of the work item</returns>
		/// On timeout throws WorkItemTimeoutException
		/// On cancel throws WorkItemCancelException
		object GetResult(			
			int millisecondsTimeout,
			bool exitContext,
			WaitHandle cancelWaitHandle,
			out Exception e);

        /// <summary>
        /// Get the result of the work item.
        /// If the work item didn't run yet then the caller waits until timeout or until the cancelWaitHandle is signaled.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <param name="exitContext">if set to <c>true</c> [exit context].</param>
        /// <param name="cancelWaitHandle">The cancel wait handle.</param>
        /// <param name="e">Filled with the exception if one was thrown</param>
        /// <returns>The result of the work item</returns>
        /// On timeout throws WorkItemTimeoutException
        /// On cancel throws WorkItemCancelException
		object GetResult(			
			TimeSpan timeout,
			bool exitContext,
			WaitHandle cancelWaitHandle,
			out Exception e);

		/// <summary>
		/// Gets an indication whether the asynchronous operation has completed.
		/// </summary>
		bool IsCompleted { get; }

		/// <summary>
		/// Gets an indication whether the asynchronous operation has been canceled.
		/// </summary>
		bool IsCanceled { get; }

		/// <summary>
		/// Gets a user-defined object that qualifies or contains information about an asynchronous operation.
		/// </summary>
		object State { get; }

		/// <summary>
		/// Cancel the work item if it didn't start running yet.
		/// </summary>
		/// <returns>Returns true on success or false if the work item is in progress or already completed</returns>
		bool Cancel();

		/// <summary>
		/// Get the work item's priority
		/// </summary>
		WorkItemPriority WorkItemPriority { get; }

		/// <summary>
		/// Return the result, same as GetResult()
		/// </summary>
		object Result { get; }

		/// <summary>
		/// Returns the exception if occured otherwise returns null.
		/// </summary>
		object Exception { get; }
	}

	#endregion
}
