using System;
using System.Reflection;
using Exceptionless.Core;
using Foundatio.Logging;
using Foundatio.ServiceProviders;
using SimpleInjector;

namespace Exceptionless.Api.Tests.Jobs {
    public class JobBootstrapper : BootstrappedServiceProviderBase {
        protected override IServiceProvider BootstrapInternal(ILoggerFactory loggerFactory) {
            loggerFactory = loggerFactory ?? Settings.Current.GetLoggerFactory();
            var logger = loggerFactory.CreateLogger<JobBootstrapper>();

            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            
            Core.Bootstrapper.RegisterServices(container, loggerFactory);

            Assembly insulationAssembly = null;
            try {
                insulationAssembly = Assembly.Load("Exceptionless.Insulation");
            } catch (Exception ex) {
                logger.Error().Message("Unable to load the insulation assembly.").Exception(ex).Write();
            }

            if (insulationAssembly != null) {
                var bootstrapperType = insulationAssembly.GetType("Exceptionless.Insulation.Bootstrapper");
                if (bootstrapperType == null)
                    return container;

                bootstrapperType.GetMethod("RegisterServices", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { container, loggerFactory });
            }

            return container;
        }
    }
}