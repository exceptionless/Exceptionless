// Ami Bar
// amibar@gmail.com
//
// Smart thread pool in C#.
// 7 Aug 2004 - Initial release
// 14 Sep 2004 - Bug fixes 
// 15 Oct 2004 - Added new features
//		- Work items return result.
//		- Support waiting synchronization for multiple work items.
//		- Work items can be cancelled.
//		- Passage of the caller thread’s context to the thread in the pool.
//		- Minimal usage of WIN32 handles.
//		- Minor bug fixes.
// 26 Dec 2004 - Changes:
//		- Removed static constructors.
//      - Added finalizers.
//		- Changed Exceptions so they are serializable.
//		- Fixed the bug in one of the SmartThreadPool constructors.
//		- Changed the SmartThreadPool.WaitAll() so it will support any number of waiters. 
//        The SmartThreadPool.WaitAny() is still limited by the .NET Framework.
//		- Added PostExecute with options on which cases to call it.
//      - Added option to dispose of the state objects.
//      - Added a WaitForIdle() method that waits until the work items queue is empty.
//      - Added an STPStartInfo class for the initialization of the thread pool.
//      - Changed exception handling so if a work item throws an exception it 
//        is rethrown at GetResult(), rather then firing an UnhandledException event.
//        Note that PostExecute exception are always ignored.
// 25 Mar 2005 - Changes:
//		- Fixed lost of work items bug
// 3 Jul 2005: Changes.
//      - Fixed bug where Enqueue() throws an exception because PopWaiter() returned null, hardly reconstructed. 
// 16 Aug 2005: Changes.
//		- Fixed bug where the InUseThreads becomes negative when canceling work items. 
//
// 31 Jan 2006 - Changes:
//		- Added work items priority
//		- Removed support of chained delegates in callbacks and post executes (nobody really use this)
//		- Added work items groups
//		- Added work items groups idle event
//		- Changed SmartThreadPool.WaitAll() behavior so when it gets empty array
//		  it returns true rather then throwing an exception.
//		- Added option to start the STP and the WIG as suspended
//		- Exception behavior changed, the real exception is returned by an 
//		  inner exception
//		- Added option to keep the Http context of the caller thread. (Thanks to Steven T.)
//		- Added performance counters
//		- Added priority to the threads in the pool
//
// 13 Feb 2006 - Changes:
//		- Added a call to the dispose of the Performance Counter so
//		  their won't be a Performance Counter leak.
//		- Added exception catch in case the Performance Counters cannot 
//		  be created.

using System;
using System.Security;
using System.Threading;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using CodeSmith.Core.Threading.Internal;

namespace CodeSmith.Core.Threading
{
	#region SmartThreadPool class
	/// <summary>
	/// Smart thread pool class.
	/// </summary>
	public class SmartThreadPool : IWorkItemsGroup, IDisposable
	{
		#region Default Constants

		/// <summary>
		/// Default minimum number of threads the thread pool contains. (0)
		/// </summary>
		public const int DefaultMinWorkerThreads = 0;

		/// <summary>
		/// Default maximum number of threads the thread pool contains. (25)
		/// </summary>
		public const int DefaultMaxWorkerThreads = 25;

		/// <summary>
		/// Default idle timeout in milliseconds. (One minute)
		/// </summary>
		public const int DefaultIdleTimeout = 60*1000; // One minute

		/// <summary>
		/// Indicate to copy the security context of the caller and then use it in the call. (false)
		/// </summary>
		public const bool DefaultUseCallerCallContext = false; 

		/// <summary>
		/// Indicate to copy the HTTP context of the caller and then use it in the call. (false)
		/// </summary>
		public const bool DefaultUseCallerHttpContext = false;

		/// <summary>
		/// Indicate to dispose of the state objects if they support the IDispose interface. (false)
		/// </summary>
		public const bool DefaultDisposeOfStateObjects = false; 

		/// <summary>
		/// The default option to run the post execute
		/// </summary>
		public const CallToPostExecute DefaultCallToPostExecute = CallToPostExecute.Always;

		/// <summary>
		/// The default post execute method to run. 
		/// When null it means not to call it.
		/// </summary>
		public static readonly PostExecuteWorkItemCallback DefaultPostExecuteWorkItemCallback = null;

		/// <summary>
		/// The default work item priority
		/// </summary>
		public const WorkItemPriority DefaultWorkItemPriority = WorkItemPriority.Normal;

		/// <summary>
		/// The default is to work on work items as soon as they arrive
		/// and not to wait for the start.
		/// </summary>
		public const bool DefaultStartSuspended = false;

		/// <summary>
		/// The default is not to use the performance counters
		/// </summary>
		public static readonly string DefaultPerformanceCounterInstanceName = null;

		/// <summary>
		/// The default thread priority
		/// </summary>
		public const ThreadPriority DefaultThreadPriority = ThreadPriority.Normal;

		#endregion

		#region Member Variables

		/// <summary>
		/// Contains the name of this instance of SmartThreadPool.
		/// Can be changed by the user.
		/// </summary>
		private string _name = "SmartThreadPool";

		/// <summary>
		/// Hashtable of all the threads in the thread pool.
		/// </summary>
		private Hashtable _workerThreads = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// Queue of work items.
		/// </summary>
		private WorkItemsQueue _workItemsQueue = new WorkItemsQueue();

		/// <summary>
		/// Count the work items handled.
		/// Used by the performance counter.
		/// </summary>
		private long _workItemsProcessed = 0;

		/// <summary>
		/// Number of threads that currently work (not idle).
		/// </summary>
		private int _inUseWorkerThreads = 0;

		/// <summary>
		/// Start information to use. 
		/// It is simpler than providing many constructors.
		/// </summary>
		private STPStartInfo _stpStartInfo = new STPStartInfo();

		/// <summary>
		/// Total number of work items that are stored in the work items queue 
		/// plus the work items that the threads in the pool are working on.
		/// </summary>
		private int _currentWorkItemsCount = 0;

		/// <summary>
		/// Signaled when the thread pool is idle, i.e. no thread is busy
		/// and the work items queue is empty
		/// </summary>
		private ManualResetEvent _isIdleWaitHandle = new ManualResetEvent(true);

		/// <summary>
		/// An event to signal all the threads to quit immediately.
		/// </summary>
		private ManualResetEvent _shuttingDownEvent = new ManualResetEvent(false);

		/// <summary>
		/// A flag to indicate the threads to quit.
		/// </summary>
		private bool _shutdown = false;

		/// <summary>
		/// Counts the threads created in the pool.
		/// It is used to name the threads.
		/// </summary>
		private int _threadCounter = 0;

		/// <summary>
		/// Indicate that the SmartThreadPool has been disposed
		/// </summary>
		private bool _isDisposed = false;

		/// <summary>
		/// Event to send that the thread pool is idle
		/// </summary>
#pragma warning disable 67
		private event EventHandler _stpIdle;
#pragma warning restore 67

        ///// <summary>
        ///// On idle event
        ///// </summary>
		//private event WorkItemsGroupIdleHandler _onIdle;

		/// <summary>
		/// Holds all the WorkItemsGroup instances that have at least one 
		/// work item int the SmartThreadPool
		/// This variable is used in case of Shutdown
		/// </summary>
		private Hashtable _workItemsGroups = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// A reference from each thread in the thread pool to its SmartThreadPool
		/// object container.
		/// With this variable a thread can know whatever it belongs to a 
		/// SmartThreadPool.
		/// </summary>
		[ThreadStatic]
		private static SmartThreadPool _smartThreadPool;

		/// <summary>
		/// A reference to the current work item a thread from the thread pool 
		/// is executing.
		/// </summary>
		[ThreadStatic]
		private static WorkItem _currentWorkItem;

		/// <summary>
		/// STP performance counters
		/// </summary>
		private ISTPInstancePerformanceCounters _pcs = NullSTPInstancePerformanceCounters.Instance;

		#endregion

		#region Construction and Finalization

		/// <summary>
		/// Constructor
		/// </summary>
		public SmartThreadPool()
		{
			Initialize();
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="idleTimeout">Idle timeout in milliseconds</param>
		public SmartThreadPool(int idleTimeout)
		{
			_stpStartInfo.IdleTimeout = idleTimeout;
			Initialize();
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="idleTimeout">Idle timeout in milliseconds</param>
		/// <param name="maxWorkerThreads">Upper limit of threads in the pool</param>
		public SmartThreadPool(
			int idleTimeout,
			int maxWorkerThreads)
		{
			_stpStartInfo.IdleTimeout = idleTimeout;
			_stpStartInfo.MaxWorkerThreads = maxWorkerThreads;
			Initialize();
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="idleTimeout">Idle timeout in milliseconds</param>
		/// <param name="maxWorkerThreads">Upper limit of threads in the pool</param>
		/// <param name="minWorkerThreads">Lower limit of threads in the pool</param>
		public SmartThreadPool(
			int idleTimeout,
			int maxWorkerThreads,
			int minWorkerThreads)
		{
			_stpStartInfo.IdleTimeout = idleTimeout;
			_stpStartInfo.MaxWorkerThreads = maxWorkerThreads;
			_stpStartInfo.MinWorkerThreads = minWorkerThreads;
			Initialize();
		}

		/// <summary>
		/// Constructor
		/// </summary>
		public SmartThreadPool(STPStartInfo stpStartInfo)
		{
			_stpStartInfo = new STPStartInfo(stpStartInfo);
			Initialize();
		}

		private void Initialize()
		{
			ValidateSTPStartInfo();

			if (null != _stpStartInfo.PerformanceCounterInstanceName)
			{
				try
				{
					_pcs = new STPInstancePerformanceCounters(_stpStartInfo.PerformanceCounterInstanceName);
				}
				catch(Exception e)
				{
					Debug.WriteLine("Unable to create Performance Counters: " + e.ToString());
					_pcs = NullSTPInstancePerformanceCounters.Instance;
				}
			}

			StartOptimalNumberOfThreads();
		}

		private void StartOptimalNumberOfThreads()
		{
			int threadsCount = Math.Max(_workItemsQueue.Count, _stpStartInfo.MinWorkerThreads);
			threadsCount = Math.Min(threadsCount, _stpStartInfo.MaxWorkerThreads);
			StartThreads(threadsCount);
		}

		private void ValidateSTPStartInfo()
		{
			if (_stpStartInfo.MinWorkerThreads < 0)
			{
				throw new ArgumentOutOfRangeException(
					"MinWorkerThreads", "MinWorkerThreads cannot be negative");
			}

			if (_stpStartInfo.MaxWorkerThreads <= 0)
			{
				throw new ArgumentOutOfRangeException(
					"MaxWorkerThreads", "MaxWorkerThreads must be greater than zero");
			}

			if (_stpStartInfo.MinWorkerThreads > _stpStartInfo.MaxWorkerThreads)
			{
				throw new ArgumentOutOfRangeException(
					"MinWorkerThreads, maxWorkerThreads", 
					"MaxWorkerThreads must be greater or equal to MinWorkerThreads");
			}
		}

		private void ValidateCallback(Delegate callback)
		{
			if (callback.GetInvocationList().Length > 1)
			{
				throw new NotSupportedException("SmartThreadPool doesn't support delegates chains");
			}
		}

		#endregion

		#region Thread Processing

		/// <summary>
		/// Waits on the queue for a work item, shutdown, or timeout.
		/// </summary>
		/// <returns>
		/// Returns the WaitingCallback or null in case of timeout or shutdown.
		/// </returns>
		private WorkItem Dequeue()
		{
			WorkItem workItem = 
				_workItemsQueue.DequeueWorkItem(_stpStartInfo.IdleTimeout, _shuttingDownEvent);

			return workItem;
		}

		/// <summary>
		/// Put a new work item in the queue
		/// </summary>
		/// <param name="workItem">A work item to queue</param>
		private void Enqueue(WorkItem workItem)
		{
			Enqueue(workItem, true);
		}

        /// <summary>
        /// Put a new work item in the queue
        /// </summary>
        /// <param name="workItem">A work item to queue</param>
        /// <param name="incrementWorkItems">if set to <c>true</c> increment work items.</param>
		internal void Enqueue(WorkItem workItem, bool incrementWorkItems)
		{
			// Make sure the workItem is not null
			Debug.Assert(null != workItem);

			if (incrementWorkItems)
			{
				IncrementWorkItemsCount();
			}

			_workItemsQueue.EnqueueWorkItem(workItem);
			workItem.WorkItemIsQueued();

			// If all the threads are busy then try to create a new one
			if ((InUseThreads + WaitingCallbacks) > _workerThreads.Count) 
			{
				StartThreads(1);
			}
		}

		private void IncrementWorkItemsCount()
		{
			_pcs.SampleWorkItems(_workItemsQueue.Count, _workItemsProcessed);

			int count = Interlocked.Increment(ref _currentWorkItemsCount);
			//Trace.WriteLine("WorkItemsCount = " + _currentWorkItemsCount.ToString());
			if (count == 1) 
			{
				//Trace.WriteLine("STP is NOT idle");
				_isIdleWaitHandle.Reset();
			}
		}

		private void DecrementWorkItemsCount()
		{
			++_workItemsProcessed;

			// The counter counts even if the work item was cancelled
			_pcs.SampleWorkItems(_workItemsQueue.Count, _workItemsProcessed);

			int count = Interlocked.Decrement(ref _currentWorkItemsCount);
			//Trace.WriteLine("WorkItemsCount = " + _currentWorkItemsCount.ToString());
			if (count == 0) 
			{
				//Trace.WriteLine("STP is idle");
				_isIdleWaitHandle.Set();
			}
		}

		internal void RegisterWorkItemsGroup(IWorkItemsGroup workItemsGroup)
		{
			_workItemsGroups[workItemsGroup] = workItemsGroup;
		}

		internal void UnregisterWorkItemsGroup(IWorkItemsGroup workItemsGroup)
		{
			if (_workItemsGroups.Contains(workItemsGroup))
			{
				_workItemsGroups.Remove(workItemsGroup);
			}
		}

		/// <summary>
		/// Inform that the current thread is about to quit or quiting.
		/// The same thread may call this method more than once.
		/// </summary>
		private void InformCompleted()
		{
			// There is no need to lock the two methods together 
			// since only the current thread removes itself
			// and the _workerThreads is a synchronized hashtable
			if (_workerThreads.Contains(Thread.CurrentThread))
			{
				_workerThreads.Remove(Thread.CurrentThread);
				_pcs.SampleThreads(_workerThreads.Count, _inUseWorkerThreads);
			}
		}

		/// <summary>
		/// Starts new threads
		/// </summary>
		/// <param name="threadsCount">The number of threads to start</param>
		private void StartThreads(int threadsCount)
		{
			if (_stpStartInfo.StartSuspended)
			{
				return;
			}

			lock(_workerThreads.SyncRoot)
			{
				// Don't start threads on shut down
				if (_shutdown)
				{
					return;
				}

				for(int i = 0; i < threadsCount; ++i)
				{
					// Don't create more threads then the upper limit
					if (_workerThreads.Count >= _stpStartInfo.MaxWorkerThreads)
					{
						return;
					}

					// Create a new thread
					Thread workerThread = new Thread(new ThreadStart(ProcessQueuedItems));

					// Configure the new thread and start it
					workerThread.Name = "STP " + Name + " Thread #" + _threadCounter;
					workerThread.IsBackground = true;
					workerThread.Priority = _stpStartInfo.ThreadPriority;
					workerThread.Start();
					++_threadCounter;

					// Add the new thread to the hashtable and update its creation
					// time.
					_workerThreads[workerThread] = DateTime.Now;
					_pcs.SampleThreads(_workerThreads.Count, _inUseWorkerThreads);
				}
			}
		}

		/// <summary>
		/// A worker thread method that processes work items from the work items queue.
		/// </summary>
		private void ProcessQueuedItems()
		{
			// Initialize the _smartThreadPool variable
			_smartThreadPool = this;

			try
			{
				bool bInUseWorkerThreadsWasIncremented = false;

				// Process until shutdown.
				while(!_shutdown)
				{
					// Update the last time this thread was seen alive.
					// It's good for debugging.
					_workerThreads[Thread.CurrentThread] = DateTime.Now;

					// Wait for a work item, shutdown, or timeout
					WorkItem workItem = Dequeue();

					// Update the last time this thread was seen alive.
					// It's good for debugging.
					_workerThreads[Thread.CurrentThread] = DateTime.Now;

					// On timeout or shut down.
					if (null == workItem)
					{
						// Double lock for quit.
						if (_workerThreads.Count > _stpStartInfo.MinWorkerThreads)
						{
							lock(_workerThreads.SyncRoot)
							{
								if (_workerThreads.Count > _stpStartInfo.MinWorkerThreads)
								{
									// Inform that the thread is quiting and then quit.
									// This method must be called within this lock or else
									// more threads will quit and the thread pool will go
									// below the lower limit.
									InformCompleted();
									break;
								}
							}
						}
					}

					// If we didn't quit then skip to the next iteration.
					if (null == workItem)
					{
						continue;
					}

					try 
					{
						// Initialize the value to false
						bInUseWorkerThreadsWasIncremented = false;

						// Change the state of the work item to 'in progress' if possible.
						// We do it here so if the work item has been canceled we won't 
						// increment the _inUseWorkerThreads.
						// The cancel mechanism doesn't delete items from the queue,  
						// it marks the work item as canceled, and when the work item
						// is dequeued, we just skip it.
						// If the post execute of work item is set to always or to
						// call when the work item is canceled then the StartingWorkItem()
						// will return true, so the post execute can run.
						if (!workItem.StartingWorkItem())
						{
							continue;
						}

						// Execute the callback.  Make sure to accurately
						// record how many callbacks are currently executing.
						int inUseWorkerThreads = Interlocked.Increment(ref _inUseWorkerThreads);
						_pcs.SampleThreads(_workerThreads.Count, inUseWorkerThreads);

						// Mark that the _inUseWorkerThreads incremented, so in the finally{}
						// statement we will decrement it correctly.
						bInUseWorkerThreadsWasIncremented = true;

						// Set the _currentWorkItem to the current work item
						_currentWorkItem = workItem;

						ExecuteWorkItem(workItem);
					}
					catch(Exception ex)
					{
                        ex.GetHashCode();
						// Do nothing
					}
					finally
					{
						if (null != workItem)
						{
							workItem.DisposeOfState();
						}

						// Set the _currentWorkItem to null, since we 
						// no longer run user's code.
						_currentWorkItem = null;

						// Decrement the _inUseWorkerThreads only if we had 
						// incremented it. Note the cancelled work items don't
						// increment _inUseWorkerThreads.
						if (bInUseWorkerThreadsWasIncremented)
						{
							int inUseWorkerThreads = Interlocked.Decrement(ref _inUseWorkerThreads);
							_pcs.SampleThreads(_workerThreads.Count, inUseWorkerThreads);
						}

						// Notify that the work item has been completed.
						// WorkItemsGroup may enqueue their next work item.
						workItem.FireWorkItemCompleted();

						// Decrement the number of work items here so the idle 
						// ManualResetEvent won't fluctuate.
						DecrementWorkItemsCount();
					}
				}
			} 
			catch(ThreadAbortException tae)
			{
                tae.GetHashCode();
				// Handle the abort exception gracfully.
				Thread.ResetAbort();
			}
			catch(Exception e)
			{
				Debug.Assert(null != e);
			}
			finally
			{
				InformCompleted();
			}
		}

		private void ExecuteWorkItem(WorkItem workItem)
		{
			_pcs.SampleWorkItemsWaitTime(workItem.WaitingTime);
			try
			{
				workItem.Execute();
			}
			catch
			{
				throw;
			}
			finally
			{
				_pcs.SampleWorkItemsProcessTime(workItem.ProcessTime);
			}
		}


		#endregion

		#region Public Methods

		/// <summary>
		/// Queue a work item
		/// </summary>
		/// <param name="callback">A callback to execute</param>
		/// <returns>Returns a work item result</returns>
		public IWorkItemResult QueueWorkItem(WorkItemCallback callback)
		{
			ValidateNotDisposed();
			ValidateCallback(callback);
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _stpStartInfo, callback);
			Enqueue(workItem);
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
			ValidateNotDisposed();
			ValidateCallback(callback);
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _stpStartInfo, callback, workItemPriority);
			Enqueue(workItem);
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
			ValidateNotDisposed();
			ValidateCallback(callback);
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _stpStartInfo, workItemInfo, callback);
			Enqueue(workItem);
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
			ValidateNotDisposed();
			ValidateCallback(callback);
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _stpStartInfo, callback, state);
			Enqueue(workItem);
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
			ValidateNotDisposed();
			ValidateCallback(callback);
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _stpStartInfo, callback, state, workItemPriority);
			Enqueue(workItem);
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
			ValidateNotDisposed();
			ValidateCallback(callback);
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _stpStartInfo, workItemInfo, callback, state);
			Enqueue(workItem);
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
			ValidateNotDisposed();
			ValidateCallback(callback);
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _stpStartInfo, callback, state, postExecuteWorkItemCallback);
			Enqueue(workItem);
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
			ValidateNotDisposed();
			ValidateCallback(callback);
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _stpStartInfo, callback, state, postExecuteWorkItemCallback, workItemPriority);
			Enqueue(workItem);
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
			ValidateNotDisposed();
			ValidateCallback(callback);
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _stpStartInfo, callback, state, postExecuteWorkItemCallback, callToPostExecute);
			Enqueue(workItem);
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
			ValidateNotDisposed();
			ValidateCallback(callback);
			WorkItem workItem = WorkItemFactory.CreateWorkItem(this, _stpStartInfo, callback, state, postExecuteWorkItemCallback, callToPostExecute, workItemPriority);
			Enqueue(workItem);
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
			ValidateWaitForIdle();
			return _isIdleWaitHandle.WaitOne(millisecondsTimeout, false);
		}

		private void ValidateWaitForIdle()
		{
			if (_smartThreadPool == this)
			{
				throw new NotSupportedException(
					"WaitForIdle cannot be called from a thread on its SmartThreadPool, it will cause may cause a deadlock");
			}
		}

		internal void ValidateWorkItemsGroupWaitForIdle(IWorkItemsGroup workItemsGroup)
		{
			ValidateWorkItemsGroupWaitForIdleImpl(workItemsGroup, SmartThreadPool._currentWorkItem);
			if ((null != workItemsGroup) && 
				(null != SmartThreadPool._currentWorkItem) &&
				SmartThreadPool._currentWorkItem.WasQueuedBy(workItemsGroup))
			{
				throw new NotSupportedException("WaitForIdle cannot be called from a thread on its SmartThreadPool, it will cause may cause a deadlock");
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void ValidateWorkItemsGroupWaitForIdleImpl(IWorkItemsGroup workItemsGroup, WorkItem workItem)
		{
			if ((null != workItemsGroup) && 
				(null != workItem) &&
				workItem.WasQueuedBy(workItemsGroup))
			{
				throw new NotSupportedException("WaitForIdle cannot be called from a thread on its SmartThreadPool, it will cause may cause a deadlock");
			}
		}



		/// <summary>
		/// Force the SmartThreadPool to shutdown
		/// </summary>
		public void Shutdown()
		{
			Shutdown(true, 0);
		}

		public void Shutdown(bool forceAbort, TimeSpan timeout)
		{
			Shutdown(forceAbort, (int)timeout.TotalMilliseconds);
		}

		/// <summary>
		/// Empties the queue of work items and abort the threads in the pool.
		/// </summary>
		public void Shutdown(bool forceAbort, int millisecondsTimeout)
		{
			ValidateNotDisposed();

			ISTPInstancePerformanceCounters pcs = _pcs;

			if (NullSTPInstancePerformanceCounters.Instance != _pcs)
			{
				_pcs.Dispose();
				// Set the _pcs to "null" to stop updating the performance
				// counters
				_pcs = NullSTPInstancePerformanceCounters.Instance;
			}

			Thread [] threads = null;
			lock(_workerThreads.SyncRoot)
			{
				// Shutdown the work items queue
				_workItemsQueue.Dispose();

				// Signal the threads to exit
				_shutdown = true;
				_shuttingDownEvent.Set();

				// Make a copy of the threads' references in the pool
				threads = new Thread [_workerThreads.Count];
				_workerThreads.Keys.CopyTo(threads, 0);
			}

			int millisecondsLeft = millisecondsTimeout;
			DateTime start = DateTime.Now;
			bool waitInfinitely = (Timeout.Infinite == millisecondsTimeout);
			bool timeout = false;

			// Each iteration we update the time left for the timeout.
			foreach(Thread thread in threads)
			{
				// Join don't work with negative numbers
				if (!waitInfinitely && (millisecondsLeft < 0))
				{
					timeout = true;
					break;
				}

				// Wait for the thread to terminate
				bool success = thread.Join(millisecondsLeft);
				if (!success)
				{
					timeout = true;
					break;
				}

				if (!waitInfinitely)
				{
					// Update the time left to wait
					TimeSpan ts = DateTime.Now - start;
					millisecondsLeft = millisecondsTimeout - (int)ts.TotalMilliseconds;
				}
			}

			if (timeout && forceAbort)
			{
				// Abort the threads in the pool
				foreach(Thread thread in threads)
				{
					if ((thread != null) && thread.IsAlive) 
					{
						try 
						{
							thread.Abort("Shutdown");
						}
						catch(SecurityException e)
						{
                            e.GetHashCode();
						}
						catch(ThreadStateException ex)
						{
                            ex.GetHashCode();
							// In case the thread has been terminated 
							// after the check if it is alive.
						}
					}
				}
			}

			// Dispose of the performance counters
			pcs.Dispose();
		}

		/// <summary>
		/// Wait for all work items to complete
		/// </summary>
		/// <param name="workItemResults">Array of work item result objects</param>
		/// <returns>
		/// true when every work item in workItemResults has completed; otherwise false.
		/// </returns>
		public static bool WaitAll(
			IWorkItemResult [] workItemResults)
		{
			return WaitAll(workItemResults, Timeout.Infinite, true);
		}

		/// <summary>
		/// Wait for all work items to complete
		/// </summary>
		/// <param name="workItemResults">Array of work item result objects</param>
		/// <param name="timeout">The number of milliseconds to wait, or a TimeSpan that represents -1 milliseconds to wait indefinitely. </param>
		/// <param name="exitContext">
		/// true to exit the synchronization domain for the context before the wait (if in a synchronized context), and reacquire it; otherwise, false. 
		/// </param>
		/// <returns>
		/// true when every work item in workItemResults has completed; otherwise false.
		/// </returns>
		public static bool WaitAll(
			IWorkItemResult [] workItemResults,
			TimeSpan timeout,
			bool exitContext)
		{
			return WaitAll(workItemResults, (int)timeout.TotalMilliseconds, exitContext);
		}

		/// <summary>
		/// Wait for all work items to complete
		/// </summary>
		/// <param name="workItemResults">Array of work item result objects</param>
		/// <param name="timeout">The number of milliseconds to wait, or a TimeSpan that represents -1 milliseconds to wait indefinitely. </param>
		/// <param name="exitContext">
		/// true to exit the synchronization domain for the context before the wait (if in a synchronized context), and reacquire it; otherwise, false. 
		/// </param>
		/// <param name="cancelWaitHandle">A cancel wait handle to interrupt the wait if needed</param>
		/// <returns>
		/// true when every work item in workItemResults has completed; otherwise false.
		/// </returns>
		public static bool WaitAll(
			IWorkItemResult [] workItemResults,  
			TimeSpan timeout,
			bool exitContext,
			WaitHandle cancelWaitHandle)
		{
			return WaitAll(workItemResults, (int)timeout.TotalMilliseconds, exitContext, cancelWaitHandle);
		}

		/// <summary>
		/// Wait for all work items to complete
		/// </summary>
		/// <param name="workItemResults">Array of work item result objects</param>
		/// <param name="millisecondsTimeout">The number of milliseconds to wait, or Timeout.Infinite (-1) to wait indefinitely.</param>
		/// <param name="exitContext">
		/// true to exit the synchronization domain for the context before the wait (if in a synchronized context), and reacquire it; otherwise, false. 
		/// </param>
		/// <returns>
		/// true when every work item in workItemResults has completed; otherwise false.
		/// </returns>
		public static bool WaitAll(
			IWorkItemResult [] workItemResults,  
			int millisecondsTimeout,
			bool exitContext)
		{
			return WorkItem.WaitAll(workItemResults, millisecondsTimeout, exitContext, null);
		}

		/// <summary>
		/// Wait for all work items to complete
		/// </summary>
		/// <param name="workItemResults">Array of work item result objects</param>
		/// <param name="millisecondsTimeout">The number of milliseconds to wait, or Timeout.Infinite (-1) to wait indefinitely.</param>
		/// <param name="exitContext">
		/// true to exit the synchronization domain for the context before the wait (if in a synchronized context), and reacquire it; otherwise, false. 
		/// </param>
		/// <param name="cancelWaitHandle">A cancel wait handle to interrupt the wait if needed</param>
		/// <returns>
		/// true when every work item in workItemResults has completed; otherwise false.
		/// </returns>
		public static bool WaitAll(
			IWorkItemResult [] workItemResults,  
			int millisecondsTimeout,
			bool exitContext,
			WaitHandle cancelWaitHandle)
		{
			return WorkItem.WaitAll(workItemResults, millisecondsTimeout, exitContext, cancelWaitHandle);
		}


		/// <summary>
		/// Waits for any of the work items in the specified array to complete, cancel, or timeout
		/// </summary>
		/// <param name="workItemResults">Array of work item result objects</param>
		/// <returns>
		/// The array index of the work item result that satisfied the wait, or WaitTimeout if any of the work items has been canceled.
		/// </returns>
		public static int WaitAny(
			IWorkItemResult [] workItemResults)
		{
			return WaitAny(workItemResults, Timeout.Infinite, true);
		}

		/// <summary>
		/// Waits for any of the work items in the specified array to complete, cancel, or timeout
		/// </summary>
		/// <param name="workItemResults">Array of work item result objects</param>
		/// <param name="timeout">The number of milliseconds to wait, or a TimeSpan that represents -1 milliseconds to wait indefinitely. </param>
		/// <param name="exitContext">
		/// true to exit the synchronization domain for the context before the wait (if in a synchronized context), and reacquire it; otherwise, false. 
		/// </param>
		/// <returns>
		/// The array index of the work item result that satisfied the wait, or WaitTimeout if no work item result satisfied the wait and a time interval equivalent to millisecondsTimeout has passed or the work item has been canceled.
		/// </returns>
		public static int WaitAny(
			IWorkItemResult [] workItemResults,
			TimeSpan timeout,
			bool exitContext)
		{
			return WaitAny(workItemResults, (int)timeout.TotalMilliseconds, exitContext);
		}

		/// <summary>
		/// Waits for any of the work items in the specified array to complete, cancel, or timeout
		/// </summary>
		/// <param name="workItemResults">Array of work item result objects</param>
		/// <param name="timeout">The number of milliseconds to wait, or a TimeSpan that represents -1 milliseconds to wait indefinitely. </param>
		/// <param name="exitContext">
		/// true to exit the synchronization domain for the context before the wait (if in a synchronized context), and reacquire it; otherwise, false. 
		/// </param>
		/// <param name="cancelWaitHandle">A cancel wait handle to interrupt the wait if needed</param>
		/// <returns>
		/// The array index of the work item result that satisfied the wait, or WaitTimeout if no work item result satisfied the wait and a time interval equivalent to millisecondsTimeout has passed or the work item has been canceled.
		/// </returns>
		public static int WaitAny(
			IWorkItemResult [] workItemResults,
			TimeSpan timeout,
			bool exitContext,
			WaitHandle cancelWaitHandle)
		{
			return WaitAny(workItemResults, (int)timeout.TotalMilliseconds, exitContext, cancelWaitHandle);
		}

		/// <summary>
		/// Waits for any of the work items in the specified array to complete, cancel, or timeout
		/// </summary>
		/// <param name="workItemResults">Array of work item result objects</param>
		/// <param name="millisecondsTimeout">The number of milliseconds to wait, or Timeout.Infinite (-1) to wait indefinitely.</param>
		/// <param name="exitContext">
		/// true to exit the synchronization domain for the context before the wait (if in a synchronized context), and reacquire it; otherwise, false. 
		/// </param>
		/// <returns>
		/// The array index of the work item result that satisfied the wait, or WaitTimeout if no work item result satisfied the wait and a time interval equivalent to millisecondsTimeout has passed or the work item has been canceled.
		/// </returns>
		public static int WaitAny(
			IWorkItemResult [] workItemResults,  
			int millisecondsTimeout,
			bool exitContext)
		{
			return WorkItem.WaitAny(workItemResults, millisecondsTimeout, exitContext, null);
		}

		/// <summary>
		/// Waits for any of the work items in the specified array to complete, cancel, or timeout
		/// </summary>
		/// <param name="workItemResults">Array of work item result objects</param>
		/// <param name="millisecondsTimeout">The number of milliseconds to wait, or Timeout.Infinite (-1) to wait indefinitely.</param>
		/// <param name="exitContext">
		/// true to exit the synchronization domain for the context before the wait (if in a synchronized context), and reacquire it; otherwise, false. 
		/// </param>
		/// <param name="cancelWaitHandle">A cancel wait handle to interrupt the wait if needed</param>
		/// <returns>
		/// The array index of the work item result that satisfied the wait, or WaitTimeout if no work item result satisfied the wait and a time interval equivalent to millisecondsTimeout has passed or the work item has been canceled.
		/// </returns>
		public static int WaitAny(
			IWorkItemResult [] workItemResults,  
			int millisecondsTimeout,
			bool exitContext,
			WaitHandle cancelWaitHandle)
		{
			return WorkItem.WaitAny(workItemResults, millisecondsTimeout, exitContext, cancelWaitHandle);
		}

		public IWorkItemsGroup CreateWorkItemsGroup(int concurrency)
		{
			IWorkItemsGroup workItemsGroup = new WorkItemsGroup(this, concurrency, _stpStartInfo);
			return workItemsGroup;
		}

		public IWorkItemsGroup CreateWorkItemsGroup(int concurrency, WIGStartInfo wigStartInfo)
		{
			IWorkItemsGroup workItemsGroup = new WorkItemsGroup(this, concurrency, wigStartInfo);
			return workItemsGroup;
		}

		public event WorkItemsGroupIdleHandler OnIdle
		{
			add
			{
				throw new NotImplementedException("This event is not implemented in the SmartThreadPool class. Please create a WorkItemsGroup in order to use this feature.");
				//_onIdle += value;
			}
			remove
			{
				throw new NotImplementedException("This event is not implemented in the SmartThreadPool class. Please create a WorkItemsGroup in order to use this feature.");
				//_onIdle -= value;
			}
		}

		public void Cancel()
		{
			ICollection workItemsGroups = _workItemsGroups.Values;
			foreach(WorkItemsGroup workItemsGroup in workItemsGroups)
			{
				workItemsGroup.Cancel();
			}
		}

		public void Start()
		{
			lock (this)
			{
				if (!this._stpStartInfo.StartSuspended)
				{
					return;
				}
				_stpStartInfo.StartSuspended = false;
			}
			
			ICollection workItemsGroups = _workItemsGroups.Values;
			foreach(WorkItemsGroup workItemsGroup in workItemsGroups)
			{
				workItemsGroup.OnSTPIsStarting();
			}

			StartOptimalNumberOfThreads();
		}

		#endregion

		#region Properties

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
		/// Get the lower limit of threads in the pool.
		/// </summary>
		public int MinThreads 
		{ 
			get 
			{
				ValidateNotDisposed();
				return _stpStartInfo.MinWorkerThreads; 
			}
		}

		/// <summary>
		/// Get the upper limit of threads in the pool.
		/// </summary>
		public int MaxThreads 
		{ 
			get 
			{
				ValidateNotDisposed();
				return _stpStartInfo.MaxWorkerThreads; 
			} 
		}
		/// <summary>
		/// Get the number of threads in the thread pool.
		/// Should be between the lower and the upper limits.
		/// </summary>
		public int ActiveThreads 
		{ 
			get 
			{
				ValidateNotDisposed();
				return _workerThreads.Count; 
			} 
		}

		/// <summary>
		/// Get the number of busy (not idle) threads in the thread pool.
		/// </summary>
		public int InUseThreads 
		{ 
			get 
			{ 
				ValidateNotDisposed();
				return _inUseWorkerThreads; 
			} 
		}

		/// <summary>
		/// Get the number of work items in the queue.
		/// </summary>
		public int WaitingCallbacks 
		{ 
			get 
			{ 
				ValidateNotDisposed();
				return _workItemsQueue.Count;
			} 
		}


		public event EventHandler Idle
		{
			add
			{
				_stpIdle += value;
			}

			remove
			{
				_stpIdle -= value;
			}
		}

        #endregion

        #region IDisposable Members

        ~SmartThreadPool()
        {
            Dispose();
		}

        public void Dispose()
        {
            if (!_isDisposed)
            {
                if (!_shutdown)
                {
                    Shutdown();
                }

                if (null != _shuttingDownEvent)
                {
                    _shuttingDownEvent.Close();
                    _shuttingDownEvent = null;
                }
                _workerThreads.Clear();
                _isDisposed = true;
                GC.SuppressFinalize(this);
            }
        }

        private void ValidateNotDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().ToString(), "The SmartThreadPool has been shutdown");
            }
        }
        #endregion
    }
	#endregion
}
