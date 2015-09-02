using System;
using SimpleInjector;
using SimpleInjector.Advanced;
using SimpleInjector.Packaging;

namespace Exceptionless.Core.Extensions {
    public static class ContainerExtensions {
        public static void RegisterPackage<TPackage>(this Container container) {
            if (container == null)
                throw new ArgumentNullException("container");

            var package = Activator.CreateInstance(typeof(TPackage)) as IPackage;
            if (package == null)
                throw new ArgumentException("TPackage must implement IPackage.");

            package.RegisterServices(container);
        }

        public static void RegisterSingleImplementation<TImplementation>(this Container container, params Type[] serviceTypesToRegisterFor) {
            var implementationType = typeof(TImplementation);
            var registration = Lifestyle.Singleton.CreateRegistration(implementationType, implementationType, container);
            foreach (var serviceType in serviceTypesToRegisterFor)
                container.AddRegistration(serviceType, registration);
        }

        public static void Bootstrap<T>(this Container container, T target) {
            foreach (var configuration in container.GetAllInstances<Action<T>>()) {
                configuration(target);
            }
        }

        public static void AddBootstrapper<T>(this Container container, Action<T> configuration) {
            var tran = Lifestyle.Transient;
            var type = typeof(Action<T>);

            container.AppendToCollection(type, tran.CreateRegistration(type, () => configuration, container));
        }
    }
}