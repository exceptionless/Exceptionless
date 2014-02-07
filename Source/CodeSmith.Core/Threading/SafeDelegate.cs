#if !SILVERLIGHT && !PFX_LEGACY_3_5
using System;

namespace CodeSmith.Core.Threading
{
    /// <summary>
    /// Class that implements a wrapper for a delegate to support 
    /// fire and forget asynchronous invoke of a delegate.
    /// </summary>
    public static class SafeDelegate
    {
        private delegate void DelegateWrapper(Delegate d, object[] args);

        private static readonly DelegateWrapper _wrapperInstance = InvokeWrappedDelegate;
        private static readonly AsyncCallback _callback = EndWrapperInvoke;

        /// <summary>
        /// Invoke the specified delegate with the specified arguments
        /// asynchronously on a thread pool thread. EndInvoke is automatically 
        /// called to prevent resource leaks.
        /// </summary>
        public static void InvokeAsync(Delegate d, params object[] args)
        {
            // Invoke the wrapper asynchronously, which will then
            // execute the wrapped delegate synchronously (in the
            // thread pool thread)
            _wrapperInstance.BeginInvoke(d, args, _callback, null);
        }

        private static void InvokeWrappedDelegate(Delegate d, object[] args)
        {
            d.DynamicInvoke(args);
        }

        private static void EndWrapperInvoke(IAsyncResult ar)
        {
            _wrapperInstance.EndInvoke(ar);
            var waitHandle = ar.AsyncWaitHandle;
            if (waitHandle == null) 
                return;

            waitHandle.Close();
            waitHandle.Dispose();
        }
    }
}
#endif