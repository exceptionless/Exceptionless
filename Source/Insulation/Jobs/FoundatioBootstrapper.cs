using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Insulation.Logging;
using Exceptionless.NLog;
using Foundatio.Logging;
using Foundatio.Serializer;
using Foundatio.ServiceProviders;
using SimpleInjector;

namespace Exceptionless.Insulation.Jobs {
    public class FoundatioBootstrapper : BootstrappedServiceProviderBase {
        public override IServiceProvider Bootstrap() {
            Logger.RegisterWriter(new NLogAdapter());
            ExceptionlessClient.Default.Configuration.SetVersion(Settings.Current.Version);
            ExceptionlessClient.Default.Configuration.UseLogger(new NLogExceptionlessLog(Exceptionless.Logging.LogLevel.Warn));
            ExceptionlessClient.Default.Register();

            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            container.Options.ResolveUnregisteredCollections = true;

            container.RegisterPackage<Core.Bootstrapper>();
            container.RegisterPackage<Bootstrapper>();

            container.Register<ISerializer, JsonNetSerializer>();
            container.Verify();

            return container;
        }
    }
}