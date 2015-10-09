using System;
using System.Reflection;
using Exceptionless.Core.Extensions;
using Foundatio.Logging;
using Foundatio.ServiceProviders;
using SimpleInjector;

namespace Exceptionless.Core.Jobs {
    public class JobBootstrapper : BootstrappedServiceProviderBase {
        public override IServiceProvider Bootstrap() {
            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            
            container.RegisterPackage<Bootstrapper>();

            Assembly insulationAssembly = null;
            try {
                insulationAssembly = Assembly.Load("Exceptionless.Insulation");
            } catch (Exception ex) {
                Logger.Error().Message("Unable to load the insulation assembly.").Exception(ex).Write();
            }

            if (insulationAssembly != null)
                container.RegisterPackages(new[] { insulationAssembly });

            return container;
        }
    }
}
