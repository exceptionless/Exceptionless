using System;
using System.Reflection;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Foundatio.ServiceProvider;
using NLog.Fluent;
using SimpleInjector;

namespace Exceptionless.Core.Jobs {
    public class JobBootstrapper : BootstrappedServiceProviderBase {
        public override IServiceProvider Bootstrap() {
            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            container.Options.PropertySelectionBehavior = new InjectAttributePropertySelectionBehavior();

            container.RegisterPackage<Bootstrapper>();

            Assembly insulationAssembly = null;
            try {
                insulationAssembly = Assembly.Load("Exceptionless.Insulation");
            } catch (Exception ex) {
                Log.Error().Message("Unable to load the insulation assembly.").Exception(ex).Write();
            }

            if (insulationAssembly != null)
                container.RegisterPackages(new[] { insulationAssembly });

            return container;
        }
    }
}
