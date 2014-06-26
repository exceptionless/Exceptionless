using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Routing;
using AutoMapper;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Providers;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Serialization;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using Exceptionless.Serializer;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.Security.OAuth;
using Microsoft.Owin.StaticFiles;
using Newtonsoft.Json;
using Owin;
using SimpleInjector;
using SimpleInjector.Integration.WebApi;

namespace Exceptionless.Api {
    public static class AppBuilder {
        public static void Build(IAppBuilder app) {
            BuildWithContainer(app, CreateContainer());
        }

        public static void BuildWithContainer(IAppBuilder app, Container container) {
            if (container == null)
                throw new ArgumentNullException("container");

            var config = new HttpConfiguration();
            config.Filters.Add(new HostAuthenticationFilter(OAuthDefaults.AuthenticationType));
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.Formatters.JsonFormatter.SerializerSettings.Formatting = Formatting.Indented;
            config.Formatters.JsonFormatter.SerializerSettings.ContractResolver = new LowerCaseUnderscorePropertyNamesContractResolver();

            config.MessageHandlers.Add(new XHttpMethodOverrideDelegatingHandler());
            config.MessageHandlers.Add(new EncodingDelegatingHandler());

            // Throttle api calls to X every 15 minutes by IP address.
            config.MessageHandlers.Add(container.GetInstance<ThrottlingHandler>());

            // Reject event posts in orgs over their max event limits.
            config.MessageHandlers.Add(container.GetInstance<OverageHandler>());

            var constraintResolver = new DefaultInlineConstraintResolver();
            constraintResolver.ConstraintMap.Add("objectid", typeof(ObjectIdRouteConstraint));
            config.MapHttpAttributeRoutes(constraintResolver);

            container.RegisterWebApiFilterProvider(config);
            try {
                container.Verify();
            } catch (Exception ex) {
                var tempEx = ex;
                while (!(tempEx is ReflectionTypeLoadException)) {
                    if (tempEx.InnerException == null)
                        break;
                    tempEx = tempEx.InnerException;
                }

                var typeLoadException = tempEx as ReflectionTypeLoadException;
                if (typeLoadException != null) {
                    foreach (var loaderEx in typeLoadException.LoaderExceptions)
                        Debug.WriteLine(loaderEx.Message);
                }

                Debug.WriteLine(ex.Message);
                throw;
            }
            config.DependencyResolver = new SimpleInjectorWebApiDependencyResolver(container);
            //config.EnableSystemDiagnosticsTracing();

            var oauthProvider = container.GetInstance<ExceptionlessOAuthAuthorizationServerProvider>();
            var tokenProvider = container.GetInstance<ExceptionlessTokenProvider>();
            var authProvider = container.GetInstance<ExceptionlessOAuthBearerAuthenticationProvider>();
            app.UseOAuthAuthorizationServer(new OAuthAuthorizationServerOptions {
                TokenEndpointPath = new PathString("/token"),
                AuthorizeEndpointPath = new PathString("/account/authorize"),
                Provider = oauthProvider,
                AccessTokenExpireTimeSpan = TimeSpan.FromDays(14),
                AllowInsecureHttp = true,
                AccessTokenProvider = tokenProvider,
                RefreshTokenProvider = tokenProvider,
                AuthorizationCodeProvider = tokenProvider
            });

            app.UseOAuthBearerAuthentication(new OAuthBearerAuthenticationOptions {
                Provider = authProvider,
                AccessTokenProvider = tokenProvider,
                Realm = "Exceptionless"
            });

            app.CreatePerContext<Lazy<User>>("User", ctx => {
                if (ctx.Request.User == null || ctx.Request.User.Identity == null || !ctx.Request.User.Identity.IsAuthenticated)
                    return null;

                string userId = ctx.Request.User.GetUserId();
                if (String.IsNullOrEmpty(userId))
                    return null;

                return new Lazy<User>(() => {
                    var userRepository = container.GetInstance<IUserRepository>();
                    return userRepository.GetById(userId, true);
                });
            });

            app.CreatePerContext<Lazy<Project>>("DefaultProject", ctx => {
                if (ctx.Request.User == null || ctx.Request.User.Identity == null || !ctx.Request.User.Identity.IsAuthenticated)
                    return null;

                return new Lazy<Project>(() => {
                    string projectId = ctx.Request.User.GetDefaultProjectId();
                    var projectRepository = container.GetInstance<IProjectRepository>();

                    if (String.IsNullOrEmpty(projectId))
                        return projectRepository.GetByOrganizationIds(ctx.Request.User.GetOrganizationIds(), useCache: true).FirstOrDefault();

                    return projectRepository.GetById(projectId, true);
                });
            });

            ExceptionlessClient.Default.RegisterWebApi(config);
            app.UseCors(CorsOptions.AllowAll);
            //app.MapSignalR();
            app.UseWebApi(config);

            app.Use((context, next) => {
                if (!context.Request.Uri.AbsolutePath.StartsWith("/api"))
                    return next.Invoke();

                context.Response.Write("{\r\n   \"message\": \"not found\"\r\n}");
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;

                return Task.FromResult(0);
            });
            app.UseStageMarker(PipelineStage.PostMapHandler);

            PhysicalFileSystem fileSystem = null;
            var root = AppDomain.CurrentDomain.BaseDirectory;
            if (Directory.Exists(Path.Combine(root, "./Content")))
                fileSystem = new PhysicalFileSystem(Path.Combine(root, "./Content"));
            if (Directory.Exists(Path.Combine(root, "./bin/Content")))
                fileSystem = new PhysicalFileSystem(Path.Combine(root, "./bin/Content"));

            if (fileSystem != null)
                app.UseFileServer(new FileServerOptions { FileSystem = fileSystem });

            Mapper.Initialize(c => c.ConstructServicesUsing(container.GetInstance));

            // TODO: Remove this as it's only for testing.
            EnsureSampleData(container);
            Task.Factory.StartNew(() => container.GetInstance<ProcessEventPostsJob>().Run());
        }

        private static string _userId;
        private static void EnsureSampleData(Container container) {
            var dataHelper = container.GetInstance<DataHelper>();
            var userRepository = container.GetInstance<IUserRepository>();
            var user = userRepository.GetByEmailAddress("test@exceptionless.com");
            if (user == null)
                user = userRepository.Add(new User { EmailAddress = "test@exceptionless.com" });
            _userId = user.Id;
            dataHelper.CreateSampleOrganizationAndProject(user.Id);
        }

        public static Container CreateContainer() {
            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            container.Options.PropertySelectionBehavior = new InjectAttributePropertySelectionBehavior();

            container.RegisterPackage<Core.Bootstrapper>();
            container.RegisterPackage<Api.Bootstrapper>();

            return container;
        }
    }
}