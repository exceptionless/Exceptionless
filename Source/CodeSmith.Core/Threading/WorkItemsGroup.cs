// Ami Bar
// amibar@gmail.com

using System;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace CodeSmith.Core.Threading.Internal
{
	#region WorkItemsGroup class 

	/// <summary>
	/// Summary description for WorkItemsGroup.
	/// </summary>
	public class WorkItemsGroup : IWorkItemsGroup
	{
		#region Private members

		private object _lock = new object();
		/// <summary>
		/// Contains the name of this instance of SmartThreadPool.
		/// Can be changed by the user.
		/// </summary>
		private string _name = "WorkItemsGroup";

		/// <summary>
		/// A reference to the SmartThreadPool instance that created this 
		/// WorkItemsGroup.
		/// </summary>
		private SmartThreadPool _stp;

		/// <summary>
		/// The OnIdle event
		/// </summary>
		private event WorkItemsGroupIdleHandler _onIdle;

		/// <summary>
		/// Defines how many work items of this WorkItemsGroup can run at once.
		/// </summary>
		private int _concurrency;

		/// <summary>
		/// Priority queue to hold work items before they are passed 
		/// to the SmartThreadPool.
		/// </summary>
		private PriorityQueue _workItemsQueue;

		/// <summary>
		/// Indicate how many work items are waiting in the SmartThreadPool
		/// queue.
		/// This value is used to apply the concurrency.
		/// </summary>
		private int _workItemsInStpQueue;

		/// <summary>
		/// Indicate how many work items are currently running in the SmartThreadPool.
		/// This value is used with the Cancel, to calculate if we can send new 
		/// work items to the STP.
		/// </summary>
		private int _workItemsExecutingInStp = 0;

		/// <summary>
		/// WorkItemsGroup start information
		/// </summary>
		private WIGStartInfo _workItemsGroupStartInfo;

		/// <summary>
		/// Signaled when all of the WorkItemsGroup's work item completed.
		/// </summary>
		private ManualResetEvent _isIdleWaitHandle = new ManualResetEvent(true);

		/// <summary>
		/// A common object for all the work items that this work items group
		/// generate so we can mark them to cancel in O(1)
		/// </summary>
		private CanceledWorkItemsGroup _canceledWorkItemsGroup = new CanceledWorkItemsGroup();

		#endregion 

		#region Construction

		public WorkItemsGroup(
			SmartThreadPool stp, 
			int concurrency, 
			WIGStartInfo wigStartInfo)
		{
			if (concurrency <= 0)
			{
				throw new ArgumentOutOfRangeException("concurrency", concurrency, "concurrency must be greater than zero");
			}
			_stp = stp;
			_concurrency = concurrency;
			_workItemsGroupStartInfo = new WIGStartInfo(wigStartInfo);
			_workItemsQueue = new PriorityQueue();

			// The _workItemsInStpQueue gets the number of currently executing work items,
			// because once a work item is executing, it cannot be cancelled.
			_workItemsInStpQueue = _workItemsExecutingInStp;
		}

		#endregion 

		#region IWorkItemsGroup implementation

		/// <summary>
		/// Get/Set the name of the SmartThreadPool instance
		/// </summary>
		public string Name 
		{ 
			get
			{
				return _name;
			}

			set
			{
				_name = value;
			}
		}

		/// <summary>
		/// Queue a work item
		/// </summary>
		/// <param name="callback">A callback to execute</param>
		/// <returns>Returns a work item result</returns>
		public IWorkItemResult QueueWorkItem(WorkItemCallback callback)
		{
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _workItemsGroupStartInfo, callback);
			EnqueueToSTPNextWorkItem(workItem);
			return workItem.GetWorkItemResult();
		}

		/// <summary>
		/// Queue a work item
		/// </summary>
		/// <param name="callback">A callback to execute</param>
		/// <param name="workItemPriority">The priority of the work item</param>
		/// <returns>Returns a work item result</returns>
		public IWorkItemResult QueueWorkItem(WorkItemCallback callback, WorkItemPriority workItemPriority)
		{
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _workItemsGroupStartInfo, callback, workItemPriority);
			EnqueueToSTPNextWorkItem(workItem);
			return workItem.GetWorkItemResult();
		}

		/// <summary>
		/// Queue a work item
		/// </summary>
		/// <param name="workItemInfo">Work item info</param>
		/// <param name="callback">A callback to execute</param>
		/// <returns>Returns a work item result</returns>
		public IWorkItemResult QueueWorkItem(WorkItemInfo workItemInfo, WorkItemCallback callback)
		{
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _workItemsGroupStartInfo, workItemInfo, callback);
			EnqueueToSTPNextWorkItem(workItem);
			return workItem.GetWorkItemResult();
		}

		/// <summary>
		/// Queue a work item
		/// </summary>
		/// <param name="callback">A callback to execute</param>
		/// <param name="state">
		/// The context object of the work item. Used for passing arguments to the work item. 
		/// </param>
		/// <returns>Returns a work item result</returns>
		public IWorkItemResult QueueWorkItem(WorkItemCallback callback, object state)
		{
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _workItemsGroupStartInfo, callback, state);
			EnqueueToSTPNextWorkItem(workItem);
			return workItem.GetWorkItemResult();
		}

		/// <summary>
		/// Queue a work item
		/// </summary>
		/// <param name="callback">A callback to execute</param>
		/// <param name="state">
		/// The context object of the work item. Used for passing arguments to the work item. 
		/// </param>
		/// <param name="workItemPriority">The work item priority</param>
		/// <returns>Returns a work item result</returns>
		public IWorkItemResult QueueWorkItem(WorkItemCallback callback, object state, WorkItemPriority workItemPriority)
		{
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _workItemsGroupStartInfo, callback, state, workItemPriority);
			EnqueueToSTPNextWorkItem(workItem);
			return workItem.GetWorkItemResult();
		}

		/// <summary>
		/// Queue a work item
		/// </summary>
		/// <param name="workItemInfo">Work item information</param>
		/// <param name="callback">A callback to execute</param>
		/// <param name="state">
		/// The context object of the work item. Used for passing arguments to the work item. 
		/// </param>
		/// <returns>Returns a work item result</returns>
		public IWorkItemResult QueueWorkItem(WorkItemInfo workItemInfo, WorkItemCallback callback, object state)
		{
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _workItemsGroupStartInfo, workItemInfo, callback, state);
			EnqueueToSTPNextWorkItem(workItem);
			return workItem.GetWorkItemResult();
		}

		/// <summary>
		/// Queue a work item
		/// </summary>
		/// <param name="callback">A callback to execute</param>
		/// <param name="state">
		/// The context object of the work item. Used for passing arguments to the work item. 
		/// </param>
		/// <param name="postExecuteWorkItemCallback">
		/// A delegate to call after the callback completion
		/// </param>
		/// <returns>Returns a work item result</returns>
		public IWorkItemResult QueueWorkItem(
			WorkItemCallback callback, 
			object state,
			PostExecuteWorkItemCallback postExecuteWorkItemCallback)
		{
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _workItemsGroupStartInfo, callback, state, postExecuteWorkItemCallback);
			EnqueueToSTPNextWorkItem(workItem);
			return workItem.GetWorkItemResult();
		}

		/// <summary>
		/// Queue a work item
		/// </summary>
		/// <param name="callback">A callback to execute</param>
		/// <param name="state">
		/// The context object of the work item. Used for passing arguments to the work item. 
		/// </param>
		/// <param name="postExecuteWorkItemCallback">
		/// A delegate to call after the callback completion
		/// </param>
		/// <param name="workItemPriority">The work item priority</param>
		/// <returns>Returns a work item result</returns>
		public IWorkItemResult QueueWorkItem(
			WorkItemCallback callback, 
			object state,
			PostExecuteWorkItemCallback postExecuteWorkItemCallback,
			WorkItemPriority workItemPriority)
		{
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _workItemsGroupStartInfo, callback, state, postExecuteWorkItemCallback, workItemPriority);
			EnqueueToSTPNextWorkItem(workItem);
			return workItem.GetWorkItemResult();
		}

		/// <summary>
		/// Queue a work item
		/// </summary>
		/// <param name="callback">A callback to execute</param>
		/// <param name="state">
		/// The context object of the work item. Used for passing arguments to the work item. 
		/// </param>
		/// <param name="postExecuteWorkItemCallback">
		/// A delegate to call after the callback completion
		/// </param>
		/// <param name="callToPostExecute">Indicates on which cases to call to the post execute callback</param>
		/// <returns>Returns a work item result</returns>
		public IWorkItemResult QueueWorkItem(
			WorkItemCallback callback, 
			object state,
			PostExecuteWorkItemCallback postExecuteWorkItemCallback,
			CallToPostExecute callToPostExecute)
		{
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _workItemsGroupStartInfo, callback, state, postExecuteWorkItemCallback, callToPostExecute);
			EnqueueToSTPNextWorkItem(workItem);
			return workItem.GetWorkItemResult();
		}

		/// <summary>
		/// Queue a work item
		/// </summary>
		/// <param name="callback">A callback to execute</param>
		/// <param name="state">
		/// The context object of the work item. Used for passing arguments to the work item. 
		/// </param>
		/// <param name="postExecuteWorkItemCallback">
		/// A delegate to call after the callback completion
		/// </param>
		/// <param name="callToPostExecute">Indicates on which cases to call to the post execute callback</param>
		/// <param name="workItemPriority">The work item priority</param>
		/// <returns>Returns a work item result</returns>
		public IWorkItemResult QueueWorkItem(
			WorkItemCallback callback, 
			object state,
			PostExecuteWorkItemCallback postExecuteWorkItemCallback,
			CallToPostExecute callToPostExecute,
			WorkItemPriority workItemPriority)
		{
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _workItemsGroupStartInfo, callback, state, postExecuteWorkItemCallback, callToPostExecute, workItemPriority);
			EnqueueToSTPNextWorkItem(workItem);
			return workItem.GetWorkItemResult();
		}

		/// <summary>
		/// Wait for the thread pool to be idle
		/// </summary>
		public void WaitForIdle()
		{
			WaitForIdle(Timeout.Infinite);
		}

		/// <summary>
		/// Wait for the thread pool to be idle
		/// </summary>
		public bool WaitForIdle(TimeSpan timeout)
		{
			return WaitForIdle((int)timeout.TotalMilliseconds);
		}

		/// <summary>
		/// Wait for the thread pool to be idle
		/// </summary>
		public bool WaitForIdle(int millisecondsTimeout)
		{
			_stp.ValidateWorkItemsGroupWaitForIdle(this);
			return _isIdleWaitHandle.WaitOne(millisecondsTimeout, false);
		}

		public int WaitingCallbacks
		{
			get
			{
				return _workItemsQueue.Count;
			}
		}

		public event WorkItemsGroupIdleHandler OnIdle
		{
			add
			{
				_onIdle += value;
			}
			remove
			{
				_onIdle -= value;
			}
		}

		public void Cancel()
		{
			lock(_lock)
			{
				_canceledWorkItemsGroup.IsCanceled = true;
				_workItemsQueue.Clear();
				_workItemsInStpQueue = 0;
				_canceledWorkItemsGroup = new CanceledWorkItemsGroup();
			}
		}

		public void Start()
		{
			lock (this)
			{
				if (!_workItemsGroupStartInfo.StartSuspended)
				{
					return;
				}
				_workItemsGroupStartInfo.StartSuspended = false;
			}
			
			for(int i = 0; i < _concurrency; ++i)
			{
				EnqueueToSTPNextWorkItem(null, false);
			}
		}

		#endregion 

		#region Private methods

		private void RegisterToWorkItemCompletion(IWorkItemResult wir)
		{
			IInternalWorkItemResult iwir = wir as IInternalWorkItemResult;
			iwir.OnWorkItemStarted += new WorkItemStateCallback(OnWorkItemStartedCallback);
			iwir.OnWorkItemCompleted += new WorkItemStateCallback(OnWorkItemCompletedCallback);
		}

		public void OnSTPIsStarting()
		{
			lock (this)
			{
				if (_workItemsGroupStartInfo.StartSuspended)
				{
					return;
				}
			}
			
			for(int i = 0; i < _concurrency; ++i)
			{
				EnqueueToSTPNextWorkItem(null, false);
			}
		}

		private object FireOnIdle(object state)
		{
			FireOnIdleImpl(_onIdle);
			return null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void FireOnIdleImpl(WorkItemsGroupIdleHandler onIdle)
		{
			if (null == onIdle)
			{
				return;
			}

			Delegate[] delegates = onIdle.GetInvocationList();
			foreach(WorkItemsGroupIdleHandler eh in delegates)
			{
				try
				{
					eh(this);
				}
					// Ignore exceptions
				catch{} 
			}
		}

		private void OnWorkItemStartedCallback(WorkItem workItem)
		{
			lock(_lock)
			{
				++_workItemsExecutingInStp;
			}
		}

		private void OnWorkItemCompletedCallback(WorkItem workItem)
		{
			EnqueueToSTPNextWorkItem(null, true);
		}

		private void EnqueueToSTPNextWorkItem(WorkItem workItem)
		{
			EnqueueToSTPNextWorkItem(workItem, false);
		}

		private void EnqueueToSTPNextWorkItem(WorkItem workItem, bool decrementWorkItemsInStpQueue)
		{
			lock(_lock)
			{
				// Got here from OnWorkItemCompletedCallback()
				if (decrementWorkItemsInStpQueue)
				{
					--_workItemsInStpQueue;

					if (_workItemsInStpQueue < 0)
					{
						_workItemsInStpQueue = 0;
					}

					--_workItemsExecutingInStp;

					if (_workItemsExecutingInStp < 0)
					{
						_workItemsExecutingInStp = 0;
					}
				}

				// If the work item is not null then enqueue it
				if (null != workItem)
				{
					workItem.CanceledWorkItemsGroup = _canceledWorkItemsGroup;

					RegisterToWorkItemCompletion(workItem.GetWorkItemResult());
					_workItemsQueue.Enqueue(workItem);
					//_stp.IncrementWorkItemsCount();

					if ((1 == _workItemsQueue.Count) && 
						(0 == _workItemsInStpQueue))
					{
						_stp.RegisterWorkItemsGroup(this);
						Trace.WriteLine("WorkItemsGroup " + Name + " is NOT idle");
						_isIdleWaitHandle.Reset();
					}
				}

				// If the work items queue of the group is empty than quit
				if (0 == _workItemsQueue.Count)
				{
					if (0 == _workItemsInStpQueue)
					{
						_stp.UnregisterWorkItemsGroup(this);
						Trace.WriteLine("WorkItemsGroup " + Name + " is idle");
						_isIdleWaitHandle.Set();
						_stp.QueueWorkItem(new WorkItemCallback(this.FireOnIdle));
					}
					return;
				}

				if (!_workItemsGroupStartInfo.StartSuspended)
				{
					if (_workItemsInStpQueue < _concurrency)
					{
						WorkItem nextWorkItem = _workItemsQueue.Dequeue() as WorkItem;
						_stp.Enqueue(nextWorkItem, true);
						++_workItemsInStpQueue;
					}
				}
			}
		}

		#endregion
	}

	#endregion
}
