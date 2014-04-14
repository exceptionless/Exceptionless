using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Utility;
using Microsoft.Owin.Cors;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.Security.OAuth;
using MongoDB.Bson;
using Owin;
using SimpleInjector;
using SimpleInjector.Integration.WebApi;

namespace Exceptionless.Api {
    public static class AppBuilder {
        public static void Build(IAppBuilder app) {
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

            // sample middleware that would be how we would auth an api token
            // maybe we should be using custom OAuthBearerAuthenticationProvider's
            // http://leastprivilege.com/2013/10/31/retrieving-bearer-tokens-from-alternative-locations-in-katanaowin/
            app.Use((context, next) => {
                var token = context.Request.Query.Get("access_token");
                if (String.IsNullOrEmpty(token)) {
                    var authHeader = context.Request.Headers.Get("Authorization");
                    if (!String.IsNullOrEmpty(authHeader)) {
                        var authHeaderVal = AuthenticationHeaderValue.Parse(authHeader);
                        if (authHeaderVal.Scheme.Equals("token", StringComparison.OrdinalIgnoreCase))
                            token = authHeaderVal.Parameter;
                    }
                }
                if (token != "12345")
                    return next.Invoke();

                var claims = new List<Claim> {
                    new Claim(ClaimTypes.Name, "ApiUser"),
                    new Claim(ClaimTypes.Role, AuthorizationRoles.Client),
                    new Claim(ClaimTypes.Role, AuthorizationRoles.User),
                    new Claim(ClaimTypes.NameIdentifier, ObjectId.GenerateNewId().ToString()) // the project id
                };

                var identity = new ClaimsIdentity(claims, "ApiKey");
                var principal = new ClaimsPrincipal(identity);

                context.Request.User = principal;
                return next.Invoke();
            });
            app.UseStageMarker(PipelineStage.Authenticate);

            app.UseCors(CorsOptions.AllowAll);
            app.MapSignalR();
            app.UseWebApi(config);

            Task.Factory.StartNew(() => {
                var queue = container.GetInstance<IQueue<EventPost>>();
                while (true) {
                    try {
                        queue.DequeueAsync().ContinueWith(data => {
                            if (data.IsFaulted)
                                Debug.WriteLine("faultblake");
                            if (data.Result != null) {
                                Debug.WriteLine("completed one");
                                data.Result.CompleteAsync().Wait();
                            }
                        }).Wait();
                    } catch (Exception ex) {
                        Debug.WriteLine(ex.ToString());
                    }
                }
            });

            //Task.Factory.StartNew(() => {
            //    var queue = container.GetInstance<IQueue<EventPost>>();
            //    while (true) {
            //        queue.DequeueAsync().ContinueWith(data => {
            //            Debug.WriteLine("abandoned one");
            //            data.Result.AbandonAsync();
            //        });
            //        Thread.Sleep(1000);
            //    }
            //});
        }

        private static Container CreateContainer(HttpConfiguration config) {
            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            container.Options.PropertySelectionBehavior = new InjectAttributePropertySelectionBehavior();

            container.RegisterPackage<Bootstrapper>();
            container.RegisterWebApiFilterProvider(config);

            return container;
        }
    }
}