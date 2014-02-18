using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Exceptionless.Dependency {
    public class DefaultDependencyResolver : IDependencyResolver {
        private readonly Dictionary<Type, IList<Func<object>>> _resolvers = new Dictionary<Type, IList<Func<object>>>();
        private readonly HashSet<IDisposable> _trackedDisposables = new HashSet<IDisposable>();
        private int _disposed;

        public virtual object GetService(Type serviceType) {
            if (serviceType == null)
                throw new ArgumentNullException("serviceType");

            IList<Func<object>> activators;
            if (!_resolvers.TryGetValue(serviceType, out activators))
                return null;

            if (activators.Count == 0)
                return null;
            
            if (activators.Count > 1)
                throw new InvalidOperationException(String.Format("", serviceType.FullName));
            
            return Track(activators[0]);
        }

        public virtual IEnumerable<object> GetServices(Type serviceType) {
            IList<Func<object>> activators;
            if (!_resolvers.TryGetValue(serviceType, out activators))
                return null;

            return activators.Count == 0 ? null : activators.Select(Track).ToList();
        }

        public virtual void Register(Type serviceType, Func<object> activator) {
            IList<Func<object>> activators;
            if (!_resolvers.TryGetValue(serviceType, out activators)) {
                activators = new List<Func<object>>();
                _resolvers.Add(serviceType, activators);
            } else {
                activators.Clear();
            }
            activators.Add(activator);
        }

        public virtual void Register(Type serviceType, IEnumerable<Func<object>> activators) {
            if (activators == null)
                throw new ArgumentNullException("activators");

            IList<Func<object>> list;
            if (!_resolvers.TryGetValue(serviceType, out list)) {
                list = new List<Func<object>>();
                _resolvers.Add(serviceType, list);
            } else {
                list.Clear();
            }

            foreach (var a in activators)
                list.Add(a);
        }

        private object Track(Func<object> creator) {
            object obj = creator();

            if (_disposed != 0)
                return obj;

            var disposable = obj as IDisposable;
            if (disposable == null)
                return obj;

            lock (_trackedDisposables) {
                if (_disposed == 0)
                    _trackedDisposables.Add(disposable);
            }

            return obj;
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposing)
                return;

            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            lock (_trackedDisposables) {
                foreach (var d in _trackedDisposables)
                    d.Dispose();

                _trackedDisposables.Clear();
            }
        }

        public void Dispose() {
            Dispose(true);
        }
    }
}
