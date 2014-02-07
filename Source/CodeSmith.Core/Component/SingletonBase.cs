using System;
using System.Diagnostics;

#if PFX_LEGACY_3_5
using CodeSmith.Core.Threading;
#else
using System.Collections.Concurrent;
#endif

namespace CodeSmith.Core.Component
{
    /// <summary>
    /// A class representing a singleton pattern.
    /// </summary>
    /// <typeparam name="T">The type of the singleton</typeparam>
    public abstract class SingletonBase<T> where T : class
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        protected SingletonBase()
        { }

        private static readonly Lazy<T> _instance = new Lazy<T>(() => (T)Activator.CreateInstance(typeof(T), true));

        /// <summary>
        /// Gets the current instance of the singleton.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [DebuggerNonUserCode]
        public static T Current
        {
            get { return _instance.Value; }
        }
    }
}
