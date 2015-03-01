using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Foundatio.ServiceProvider;
using SimpleInjector;

namespace Exceptionless.Core.Jobs {
    public class JobBootstrapper : BootstrappedServiceProviderBase {
        public override IServiceProvider Bootstrap() {
            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            container.Options.PropertySelectionBehavior = new InjectAttributePropertySelectionBehavior();

            container.RegisterPackage<Bootstrapper>();

            return container;
        }
    }
}
