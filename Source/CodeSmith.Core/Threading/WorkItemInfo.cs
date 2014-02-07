// Ami Bar
// amibar@gmail.com

namespace CodeSmith.Core.Threading
{
	#region WorkItemInfo class

	/// <summary>
	/// Summary description for WorkItemInfo.
	/// </summary>
	public class WorkItemInfo
	{
		/// <summary>
		/// Use the caller's security context
		/// </summary>
		private bool _useCallerCallContext;

		/// <summary>
		/// Use the caller's security context
		/// </summary>
		private bool _useCallerHttpContext;

		/// <summary>
		/// Dispose of the state object of a work item
		/// </summary>
		private bool _disposeOfStateObjects;

		/// <summary>
		/// The option to run the post execute
		/// </summary>
		private CallToPostExecute _callToPostExecute;

		/// <summary>
		/// A post execute callback to call when none is provided in 
		/// the QueueWorkItem method.
		/// </summary>
		private PostExecuteWorkItemCallback _postExecuteWorkItemCallback;
        
		/// <summary>
		/// The priority of the work item
		/// </summary>
		private WorkItemPriority _workItemPriority;

		public WorkItemInfo()
		{
			_useCallerCallContext = SmartThreadPool.DefaultUseCallerCallContext;
			_useCallerHttpContext = SmartThreadPool.DefaultUseCallerHttpContext;
			_disposeOfStateObjects = SmartThreadPool.DefaultDisposeOfStateObjects;
			_callToPostExecute = SmartThreadPool.DefaultCallToPostExecute;
			_postExecuteWorkItemCallback = SmartThreadPool.DefaultPostExecuteWorkItemCallback;
			_workItemPriority = SmartThreadPool.DefaultWorkItemPriority;
		}

		public WorkItemInfo(WorkItemInfo workItemInfo)
		{
			_useCallerCallContext = workItemInfo._useCallerCallContext;
			_useCallerHttpContext = workItemInfo._useCallerHttpContext;
			_disposeOfStateObjects = workItemInfo._disposeOfStateObjects;
			_callToPostExecute = workItemInfo._callToPostExecute;
			_postExecuteWorkItemCallback = workItemInfo._postExecuteWorkItemCallback;
			_workItemPriority = workItemInfo._workItemPriority;
		}

		public bool UseCallerCallContext
		{
			get { return _useCallerCallContext; }
			set { _useCallerCallContext = value; }
		}

		public bool UseCallerHttpContext
		{
			get { return _useCallerHttpContext; }
			set { _useCallerHttpContext = value; }
		}

		public bool DisposeOfStateObjects
		{
			get { return _disposeOfStateObjects; }
			set { _disposeOfStateObjects = value; }
		}

		public CallToPostExecute CallToPostExecute
		{
			get { return _callToPostExecute; }
			set { _callToPostExecute = value; }
		}

		public PostExecuteWorkItemCallback PostExecuteWorkItemCallback
		{
			get { return _postExecuteWorkItemCallback; }
			set { _postExecuteWorkItemCallback = value; }
		}

		public WorkItemPriority WorkItemPriority
		{
			get { return _workItemPriority; }
			set { _workItemPriority = value; }
		}
	}

	#endregion
}
