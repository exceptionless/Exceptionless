using System;
using System.Diagnostics;

namespace Exceptionless.Core {
    public abstract class SingletonBase<T> where T : class {
        protected SingletonBase() {}

        private static readonly Lazy<T> _instance = new Lazy<T>(() => {
            var instance = (T)Activator.CreateInstance(typeof(T), true);
            var initializable = instance as IInitializable;
            if (initializable != null)
                initializable.Initialize();

            return instance;
        });

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [DebuggerNonUserCode]
        public static T Current => _instance.Value;
    }

    public interface IInitializable {
        void Initialize();
    }
}