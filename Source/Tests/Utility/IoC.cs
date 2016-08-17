using System;
using Exceptionless.Api.Tests.Authentication;
using Exceptionless.Api.Tests.Mail;
using Exceptionless.Core;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Mail;
using SimpleInjector;

namespace Exceptionless.Api.Tests.Utility {
    public static class IoC {
        private static readonly Lazy<Container> _container = new Lazy<Container>(CreateContainer);

        private static void RegisterServices(Container container) {
            container.Register<IMailer, NullMailer>();
            container.Register<IDomainLoginProvider, TestDomainLoginProvider>();
        }

        public static TService GetInstance<TService>() where TService : class {
            object result = _container.Value.GetInstance(typeof(TService));
            return result as TService;
        }

        private static Container CreateContainer() {
            var loggerFactory = Settings.Current.GetLoggerFactory();
            var logger = loggerFactory.CreateLogger(nameof(IoC));
            var container = AppBuilder.CreateContainer(loggerFactory, logger);
            RegisterServices(container);
            
            return container;
        }
    }
}