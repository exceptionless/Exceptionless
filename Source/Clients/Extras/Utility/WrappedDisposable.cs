using System;

namespace Exceptionless.Extras.Utility {
    public class WrappedDisposable<T> : IDisposable {
        private readonly Action _disposeAction;

        public WrappedDisposable(T inner, Action disposeAction = null) {
            Value = inner;
            _disposeAction = disposeAction;
        }

        public T Value { get; private set; }

        void IDisposable.Dispose() {
            var v = Value as IDisposable;
            if (v != null)
                v.Dispose();

            if (_disposeAction != null)
                _disposeAction();
        }
    }
}
