using System;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using System.Web;
using System.Runtime.Remoting.Messaging;


namespace CodeSmith.Core.Threading
{
	#region CallerThreadContext class

	/// <summary>
	/// This class stores the caller call context in order to restore
	/// it when the work item is executed in the thread pool environment. 
	/// </summary>
	internal class CallerThreadContext 
	{
		#region Prepare reflection information

		// Cached type information.
		private static MethodInfo getLogicalCallContextMethodInfo =
			typeof(Thread).GetMethod("GetLogicalCallContext", BindingFlags.Instance | BindingFlags.NonPublic);

		private static MethodInfo setLogicalCallContextMethodInfo =
			typeof(Thread).GetMethod("SetLogicalCallContext", BindingFlags.Instance | BindingFlags.NonPublic);

		private static string HttpContextSlotName = GetHttpContextSlotName();

		private static string GetHttpContextSlotName()
		{
			FieldInfo fi = typeof(HttpContext).GetField("CallContextSlotName", BindingFlags.Static | BindingFlags.NonPublic);

			if ( fi != null )
				return (string)fi.GetValue(null);
			else // Use the default "HttpContext" slot name
				return "HttpContext";
		}

		#endregion

		#region Private fields

		private HttpContext _httpContext = null;
		private LogicalCallContext _callContext = null;

		#endregion

		/// <summary>
		/// Constructor
		/// </summary>
		private CallerThreadContext()
		{
		}

		public bool CapturedCallContext
		{
			get
			{
				return (null != _callContext);
			}
		}

		public bool CapturedHttpContext
		{
			get
			{
				return (null != _httpContext);
			}
		}

		/// <summary>
		/// Captures the current thread context
		/// </summary>
		/// <returns></returns>
		public static CallerThreadContext Capture(
			bool captureCallContext, 
			bool captureHttpContext)
		{
			Debug.Assert(captureCallContext || captureHttpContext);

			CallerThreadContext callerThreadContext = new CallerThreadContext();

			// TODO: In NET 2.0, redo using the new feature of ExecutionContext class - Capture()
			// Capture Call Context
			if (captureCallContext && (getLogicalCallContextMethodInfo != null))
			{
				callerThreadContext._callContext = (LogicalCallContext)getLogicalCallContextMethodInfo.Invoke(Thread.CurrentThread, null);
				if (callerThreadContext._callContext != null)
				{
					callerThreadContext._callContext = (LogicalCallContext)callerThreadContext._callContext.Clone();
				}
			}

			// Capture httpContext
			if (captureHttpContext && (null != HttpContext.Current))
			{
				callerThreadContext._httpContext = HttpContext.Current;
			}

			return callerThreadContext;
		}

		/// <summary>
		/// Applies the thread context stored earlier
		/// </summary>
		/// <param name="callerThreadContext"></param>
		public static void Apply(CallerThreadContext callerThreadContext)
		{
			if (null == callerThreadContext) 
			{
				throw new ArgumentNullException("callerThreadContext");			
			}

			// Todo: In NET 2.0, redo using the new feature of ExecutionContext class - Run()
			// Restore call context
			if ((callerThreadContext._callContext != null) && (setLogicalCallContextMethodInfo != null))
			{
				setLogicalCallContextMethodInfo.Invoke(Thread.CurrentThread, new object[] { callerThreadContext._callContext });
			}

			// Restore HttpContext 
			if (callerThreadContext._httpContext != null)
			{
				CallContext.SetData(HttpContextSlotName, callerThreadContext._httpContext);
			}
		}
	}

	#endregion

}


/*
// Ami Bar
// amibar@gmail.com

using System;
using System.Threading;
using System.Globalization;
using System.Security.Principal;
using System.Reflection;
using System.Runtime.Remoting.Contexts;

namespace CodeSmith.Core.Threading.Internal
{
	#region CallerThreadContext class

	/// <summary>
	/// This class stores the caller thread context in order to restore
	/// it when the work item is executed in the context of the thread 
	/// from the pool.
	/// Note that we can't store the thread's CompressedStack, because 
	/// it throws a security exception
	/// </summary>
	public class CallerThreadContext
	{
		private CultureInfo _culture = null;
		private CultureInfo _cultureUI = null;
		private IPrincipal _principal;
		private System.Runtime.Remoting.Contexts.Context _context;

		private static FieldInfo _fieldInfo = GetFieldInfo();

		private static FieldInfo GetFieldInfo()
		{
			Type threadType = typeof(Thread);
			return threadType.GetField(
				"m_Context",
				BindingFlags.Instance | BindingFlags.NonPublic);
		}

		/// <summary>
		/// Constructor
		/// </summary>
		private CallerThreadContext()
		{
		}

		/// <summary>
		/// Captures the current thread context
		/// </summary>
		/// <returns></returns>
		public static CallerThreadContext Capture()
		{
			CallerThreadContext callerThreadContext = new CallerThreadContext();

			Thread thread = Thread.CurrentThread;
			callerThreadContext._culture = thread.CurrentCulture;
			callerThreadContext._cultureUI = thread.CurrentUICulture;
			callerThreadContext._principal = Thread.CurrentPrincipal;
			callerThreadContext._context = Thread.CurrentContext;
			return callerThreadContext;
		}

		/// <summary>
		/// Applies the thread context stored earlier
		/// </summary>
		/// <param name="callerThreadContext"></param>
		public static void Apply(CallerThreadContext callerThreadContext)
		{
			Thread thread = Thread.CurrentThread;
			thread.CurrentCulture = callerThreadContext._culture;
			thread.CurrentUICulture = callerThreadContext._cultureUI;
			Thread.CurrentPrincipal = callerThreadContext._principal;

			// Uncomment the following block to enable the Thread.CurrentThread
/*
			if (null != _fieldInfo)
			{
				_fieldInfo.SetValue(
					Thread.CurrentThread, 
					callerThreadContext._context);
			}
* /			
		}
	}

	#endregion
}
*/

