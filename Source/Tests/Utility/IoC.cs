using System;
using Exceptionless.Api.Tests.Mail;
using Exceptionless.Core.Mail;
using Nest;
using SimpleInjector;

namespace Exceptionless.Api.Tests.Utility {
    public static class IoC {
        private static void RegisterServices(Container container) {
            container.Register<IMailer, NullMailer>();
        }

        #region Bootstrap

        private static Container _container;
        private static bool _initialized = false;

        public static TService GetInstance<TService>() where TService : class {
            if (!_initialized)
                Initialize();

            object result = _container.GetInstance(typeof(TService));
            return result as TService;
        }

        public static Container GetContainer() {
            if (!_initialized)
                Initialize();

            return _container;
        }

        private static void Initialize() {
            _container = AppBuilder.CreateContainer(false);
            _initialized = true;

            RegisterServices(_container);

            var searchclient = _container.GetInstance<IElasticClient>();
            searchclient.DeleteIndex(i => i.AllIndices());
        }

        #endregion
    }
}