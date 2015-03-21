using System;
using Exceptionless.Api.Tests.Mail;
using Exceptionless.Core;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Migrations;
using MongoDB.Driver;
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

            if (Settings.Current.ShouldAutoUpgradeDatabase) {
                var url = new MongoUrl(Settings.Current.MongoConnectionString);
                string databaseName = url.DatabaseName;
                if (Settings.Current.AppendMachineNameToDatabase)
                    databaseName += String.Concat("-", Environment.MachineName.ToLower());

                MongoMigrationChecker.EnsureLatest(Settings.Current.MongoConnectionString, databaseName);
            }

            return container;
        }
    }
}