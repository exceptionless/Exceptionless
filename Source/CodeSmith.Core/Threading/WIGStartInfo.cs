// Ami Bar
// amibar@gmail.com

namespace CodeSmith.Core.Threading
{
	/// <summary>
	/// Summary description for WIGStartInfo.
	/// </summary>
	public class WIGStartInfo
	{
		/// <summary>
		/// Use the caller's security context
		/// </summary>
		private bool _useCallerCallContext;

		/// <summary>
		/// Use the caller's HTTP context
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
		/// Indicate the WorkItemsGroup to suspend the handling of the work items
		/// until the Start() method is called.
		/// </summary>
		private bool _startSuspended;

		public WIGStartInfo()
		{
			_useCallerCallContext = SmartThreadPool.DefaultUseCallerCallContext;
			_useCallerHttpContext = SmartThreadPool.DefaultUseCallerHttpContext;
			_disposeOfStateObjects = SmartThreadPool.DefaultDisposeOfStateObjects;
			_callToPostExecute = SmartThreadPool.DefaultCallToPostExecute;
			_postExecuteWorkItemCallback = SmartThreadPool.DefaultPostExecuteWorkItemCallback;
			_startSuspended = SmartThreadPool.DefaultStartSuspended;
		}

		public WIGStartInfo(WIGStartInfo wigStartInfo)
		{
			_useCallerCallContext = wigStartInfo._useCallerCallContext;
			_useCallerHttpContext = wigStartInfo._useCallerHttpContext;
			_disposeOfStateObjects = wigStartInfo._disposeOfStateObjects;
			_callToPostExecute = wigStartInfo._callToPostExecute;
			_postExecuteWorkItemCallback = wigStartInfo._postExecuteWorkItemCallback;
			_startSuspended = wigStartInfo._startSuspended;
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

		public bool StartSuspended
		{
			get { return _startSuspended; }
			set { _startSuspended = value; }
		}
	}
}
