using System;
using Exceptionless.Api.Tests.Mail;
using Exceptionless.Core.Mail;
using Nest;
using SimpleInjector;

namespace Exceptionless.Api.Tests.Utility {
    public static class IoC {
        private static readonly Lazy<Container> _container = new Lazy<Container>(CreateContainer);

        private static void RegisterServices(Container container) {
            container.Register<IMailer, NullMailer>();
        }

        public static TService GetInstance<TService>() where TService : class {
            object result = _container.Value.GetInstance(typeof(TService));
            return result as TService;
        }

        private static Container CreateContainer() {
            var container = AppBuilder.CreateContainer(false);
            RegisterServices(container);

            var searchclient = container.GetInstance<IElasticClient>();
            searchclient.DeleteIndex(i => i.AllIndices());
            
            return container;
        }
    }
}