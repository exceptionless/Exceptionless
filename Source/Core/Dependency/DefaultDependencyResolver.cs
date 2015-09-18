using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Exceptionless.Core.Dependency {
    public class DefaultDependencyResolver : IDependencyResolver {
        public object GetService(Type serviceType) {
            if (serviceType.IsInterface || serviceType.IsAbstract)
                return null;

            try {
                return Activator.CreateInstance(serviceType);
            } catch {
                return null;
            }
        }

        public IEnumerable<object> GetServices(Type serviceType) {
            return Enumerable.Empty<object>();
        }
    }

    public class DelegateBasedDependencyResolver : IDependencyResolver {
        private readonly Func<Type, object> _getService;
        private readonly Func<Type, IEnumerable<object>> _getServices;

        public DelegateBasedDependencyResolver(Func<Type, object> getService, Func<Type, IEnumerable<object>> getServices) {
            _getService = getService;
            _getServices = getServices;
        }

        public object GetService(Type type) {
            try {
                return _getService.Invoke(type);
            } catch {
                return null;
            }
        }

        public IEnumerable<object> GetServices(Type type) {
            return _getServices(type);
        }
    }

    public sealed class CacheDependencyResolver : IDependencyResolver {
        private readonly ConcurrentDictionary<Type, object> _cache = new ConcurrentDictionary<Type, object>();
        private readonly ConcurrentDictionary<Type, IEnumerable<object>> _cacheMultiple = new ConcurrentDictionary<Type, IEnumerable<object>>();
        private readonly Func<Type, object> _getServiceDelegate;
        private readonly Func<Type, IEnumerable<object>> _getServicesDelegate;

        private readonly IDependencyResolver _resolver;

        public CacheDependencyResolver(IDependencyResolver resolver) {
            _resolver = resolver;
            _getServiceDelegate = _resolver.GetService;
            _getServicesDelegate = _resolver.GetServices;
        }

        public object GetService(Type serviceType) {
            return _cache.GetOrAdd(serviceType, _getServiceDelegate);
        }

        public IEnumerable<object> GetServices(Type serviceType) {
            return _cacheMultiple.GetOrAdd(serviceType, _getServicesDelegate);
        }
    }

    public sealed class CommonServiceLocatorDependencyResolver : IDependencyResolver {
        private readonly Func<Type, object> _getServiceDelegate;
        private readonly Func<Type, IEnumerable<object>> _getServicesDelegate;

        public CommonServiceLocatorDependencyResolver(object commonServiceLocator) {
            if (commonServiceLocator == null)
                throw new ArgumentNullException("commonServiceLocator");

            Type locatorType = commonServiceLocator.GetType();
            MethodInfo getInstance = locatorType.GetMethod("GetInstance", new[] { typeof(Type) });
            MethodInfo getInstances = locatorType.GetMethod("GetAllInstances", new[] { typeof(Type) });

            if (getInstance == null || getInstance.ReturnType != typeof(object) || getInstances == null || getInstances.ReturnType != typeof(IEnumerable<object>)) {
                throw new ArgumentException($"{locatorType.FullName} does not implement required methods.",
                    "commonServiceLocator");
            }

            _getServiceDelegate = (Func<Type, object>)Delegate.CreateDelegate(typeof(Func<Type, object>), commonServiceLocator, getInstance);
            _getServicesDelegate = (Func<Type, IEnumerable<object>>)Delegate.CreateDelegate(typeof(Func<Type, IEnumerable<object>>), commonServiceLocator, getInstances);
        }

        public object GetService(Type serviceType) {
            return _getServiceDelegate(serviceType);
        }

        public IEnumerable<object> GetServices(Type serviceType) {
            return _getServicesDelegate(serviceType);
        }
    }
}
