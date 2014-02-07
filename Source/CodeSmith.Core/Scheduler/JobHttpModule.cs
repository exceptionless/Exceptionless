using System;
using System.Diagnostics;
using System.Threading;
using System.Web;

namespace CodeSmith.Core.Scheduler {
    /// <summary>
    /// A Http module class to start the <see cref="JobManager"/>.
    /// </summary>
    public class JobHttpModule : IHttpModule {
        private static long _initCount;

        /// <summary>
        /// Disposes of the resources (other than memory) used by the module that implements <see cref="T:System.Web.IHttpModule"/>.
        /// </summary>
        public virtual void Dispose() {
            Trace.TraceInformation("JobModule Dispose");

            //JobManager.Current.Stop();
        }

        /// <summary>
        /// Initializes a module and prepares it to handle requests.
        /// </summary>
        /// <param name="context">An <see cref="T:System.Web.HttpApplication"/> that provides access to the methods, properties, and events common to all application objects within an ASP.NET application</param>
        public virtual void Init(HttpApplication context) {
            Trace.TraceInformation("JobModule Init");

            // Ensure that this method is called only once (Singleton).
            if (Interlocked.Increment(ref _initCount) == 1)
                JobManager.Current.Start();
        }
    }
}