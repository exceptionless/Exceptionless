using System;

#if !EMBEDDED
namespace CodeSmith.Core.Component {
#else
namespace Exceptionless.Utility {
#endif
    public abstract class DisposableBase : IDisposable {
        private bool _disposed;

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected void Dispose(bool disposing) {
            if (_disposed)
                return;

            if (disposing)
                DisposeManagedResources();

            DisposeUnmanagedResources();
            _disposed = true;
        }

        /// <summary>
        /// Disposes the managed resources.
        /// </summary>
        protected virtual void DisposeManagedResources() {}

        /// <summary>
        /// Disposes the unmanaged resources.
        /// </summary>
        protected virtual void DisposeUnmanagedResources() {}

        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before the
        /// <see cref="DisposableBase"/> is reclaimed by garbage collection.
        /// </summary>
        ~DisposableBase() {
            Dispose(false);
        }
    }
}