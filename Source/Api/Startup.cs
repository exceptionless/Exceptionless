using System;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Microsoft.Owin.Cors;
using Owin;
using SimpleInjector;
using SimpleInjector.Integration.WebApi;

namespace Exceptionless.Api {
    public class Startup {
        public void Configuration(IAppBuilder builder) {
            var config = new HttpConfiguration();
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.MapHttpAttributeRoutes();

            var container = CreateContainer(config);
            config.DependencyResolver = new SimpleInjectorWebApiDependencyResolver(container);

            builder.UseCors(CorsOptions.AllowAll);
            builder.UseWebApi(config);
        }

        private Container CreateContainer(HttpConfiguration config) {
            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            container.Options.PropertySelectionBehavior = new InjectAttributePropertySelectionBehavior();

            container.RegisterPackage<Bootstrapper>();
            container.RegisterWebApiFilterProvider(config);

            return container;
        }
    }
}