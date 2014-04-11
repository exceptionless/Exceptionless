using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Microsoft.AspNet.Identity;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.OAuth;
using Owin;
using SimpleInjector;
using SimpleInjector.Integration.WebApi;

namespace Exceptionless.Api.IIS {
    public class Startup {
        public void Configuration(IAppBuilder builder) {
            var config = new HttpConfiguration();
            config.Filters.Add(new HostAuthenticationFilter(OAuthDefaults.AuthenticationType));
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.MapHttpAttributeRoutes();

            var container = CreateContainer(config);
            try {
                container.Verify();
            } catch (Exception ex) {
                Debug.WriteLine(ex.Message);
                throw;
            }
            config.DependencyResolver = new SimpleInjectorWebApiDependencyResolver(container);

            // login all users
            builder.Use((context, next) => {
                var claims = new List<Claim>();
                claims.Add(new Claim(ClaimTypes.Name, "ApiUser"));
                claims.Add(new Claim(ClaimTypes.Role, "ApiUser"));

                var identity = new ClaimsIdentity(claims, "ApiKey");
                var principal = new ClaimsPrincipal(identity);

                context.Request.User = principal;
                return next.Invoke();
            });

            builder.UseCors(CorsOptions.AllowAll);
            builder.MapSignalR();
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