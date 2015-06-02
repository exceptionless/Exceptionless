using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Cors;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using System.Web.Http.Routing;
using AutoMapper;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Security;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Serializer;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NLog.Fluent;
using Owin;
using SimpleInjector;
using SimpleInjector.Integration.WebApi;
using Swashbuckle.Application;

namespace Exceptionless.Api {
    public static class AppBuilder {
        public static void Build(IAppBuilder app) {
            BuildWithContainer(app, CreateContainer());
        }

        public static void BuildWithContainer(IAppBuilder app, Container container) {
            if (container == null)
                throw new ArgumentNullException("container");

            var contractResolver = container.GetInstance<IContractResolver>();
            var exceptionlessContractResolver = contractResolver as ExceptionlessContractResolver;
            if (exceptionlessContractResolver != null)
                exceptionlessContractResolver.UseDefaultResolverFor(typeof(Connection).Assembly);

            if (Settings.Current.ShouldAutoUpgradeDatabase)
                MongoMigrationChecker.EnsureLatest(Settings.Current.MongoConnectionString, Settings.Current.MongoDatabaseName);

            Config = new HttpConfiguration();
            Config.DependencyResolver = new SimpleInjectorWebApiDependencyResolver(container);
            Config.Formatters.Remove(Config.Formatters.XmlFormatter);
            Config.Formatters.JsonFormatter.SerializerSettings.Formatting = Formatting.Indented;
            Config.Formatters.JsonFormatter.SerializerSettings.ContractResolver = contractResolver;

            var constraintResolver = new DefaultInlineConstraintResolver();
            constraintResolver.ConstraintMap.Add("objectid", typeof(ObjectIdRouteConstraint));
            constraintResolver.ConstraintMap.Add("objectids", typeof(ObjectIdsRouteConstraint));
            constraintResolver.ConstraintMap.Add("token", typeof(TokenRouteConstraint));
            constraintResolver.ConstraintMap.Add("tokens", typeof(TokensRouteConstraint));
            Config.MapHttpAttributeRoutes(constraintResolver);

            container.RegisterWebApiFilterProvider(Config);

            VerifyContainer(container);

            container.Bootstrap(Config);
            container.Bootstrap(app);
            Log.Info().Message("Starting api...").Write();

            Config.Services.Add(typeof(IExceptionLogger), new NLogExceptionLogger());
            Config.Services.Replace(typeof(IExceptionHandler), container.GetInstance<ExceptionlessReferenceIdExceptionHandler>());

            Config.MessageHandlers.Add(container.GetInstance<XHttpMethodOverrideDelegatingHandler>());
            Config.MessageHandlers.Add(container.GetInstance<EncodingDelegatingHandler>());
            Config.MessageHandlers.Add(container.GetInstance<AuthMessageHandler>());

            // Throttle api calls to X every 15 minutes by IP address.
            Config.MessageHandlers.Add(container.GetInstance<ThrottlingHandler>());

            // Reject event posts in orgs over their max event limits.
            Config.MessageHandlers.Add(container.GetInstance<OverageHandler>());

            app.UseCors(new CorsOptions {
                    PolicyProvider = new CorsPolicyProvider
                    {
                        PolicyResolver = ctx => Task.FromResult(new CorsPolicy
                        {
                            AllowAnyHeader = true,
                            AllowAnyMethod = true,
                            AllowAnyOrigin = true,
                            SupportsCredentials = true,
                            PreflightMaxAge = 60 * 5
                        })
                    }
                });

            app.CreatePerContext<Lazy<User>>("User", ctx => new Lazy<User>(() => {
                if (ctx.Request.User == null || ctx.Request.User.Identity == null || !ctx.Request.User.Identity.IsAuthenticated)
                    return null;

                string userId = ctx.Request.User.GetUserId();
                if (String.IsNullOrEmpty(userId))
                    return null;

                var userRepository = container.GetInstance<IUserRepository>();
                return userRepository.GetById(userId, true);
            }));

            app.CreatePerContext<Lazy<Project>>("DefaultProject", ctx => new Lazy<Project>(() => {
                if (ctx.Request.User == null || ctx.Request.User.Identity == null || !ctx.Request.User.Identity.IsAuthenticated)
                    return null;

                // TODO: Use project id from url. E.G., /projects/{projectId:objectid}/events
                string projectId = ctx.Request.User.GetDefaultProjectId();
                var projectRepository = container.GetInstance<IProjectRepository>();

                if (String.IsNullOrEmpty(projectId)) {
                    var firstOrgId = ctx.Request.User.GetOrganizationIds().FirstOrDefault();
                    if (!String.IsNullOrEmpty(firstOrgId)) {
                        var project = projectRepository.GetByOrganizationId(firstOrgId, useCache: true).FirstOrDefault();
                        if (project != null)
                            return project;
                    }

                    if (Settings.Current.WebsiteMode == WebsiteMode.Dev) {
                        var dataHelper = container.GetInstance<DataHelper>();
                        // create a default org and project
                        projectId = dataHelper.CreateDefaultOrganizationAndProject(ctx.Request.GetUser());
                    }
                }

                if (String.IsNullOrEmpty(projectId))
                    return null;

                return projectRepository.GetById(projectId, true);
            }));

            app.UseWebApi(Config);
            var resolver = new SimpleInjectorSignalRDependencyResolver(container);
            if (Settings.Current.EnableRedis)
                resolver.UseRedis(new RedisScaleoutConfiguration(Settings.Current.RedisConnectionString, "exceptionless.signalr"));
            app.MapSignalR("/api/v2/push", new HubConfiguration { Resolver = resolver });

            Config.EnableSwagger("schema/{apiVersion}", c => {
                c.SingleApiVersion("v2", "Exceptionless");
                c.ApiKey("access_token").In("header").Name("access_token").Description("API Key Authentication");
                c.BasicAuth("basic").Description("Basic HTTP Authentication");
                c.IncludeXmlComments(String.Format(@"{0}\bin\Exceptionless.Api.xml", AppDomain.CurrentDomain.BaseDirectory));
                c.IgnoreObsoleteActions();
                c.DocumentFilter<FilterRoutesDocumentFilter>();
            }).EnableSwaggerUi("docs/{*assetPath}", c => {
                c.InjectStylesheet(typeof(AppBuilder).Assembly, "Exceptionless.Api.Content.docs.css");
                c.InjectJavaScript(typeof(AppBuilder).Assembly, "Exceptionless.Api.Content.docs.js");
            });

            Mapper.Configuration.ConstructServicesUsing(container.GetInstance);

            var context = new OwinContext(app.Properties);
            var token = context.Get<CancellationToken>("host.OnAppDisposing");

            CreateSampleData(container);

            if (Settings.Current.RunJobsInProcess) {
                Task.Factory.StartNew(() => container.GetInstance<EventPostsJob>().RunContinuousAsync(cancellationToken: token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default).IgnoreExceptions();
                Task.Factory.StartNew(() => container.GetInstance<EventUserDescriptionsJob>().RunContinuousAsync(cancellationToken: token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default).IgnoreExceptions();
                Task.Factory.StartNew(() => container.GetInstance<MailMessageJob>().RunContinuousAsync(cancellationToken: token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default).IgnoreExceptions();
                Task.Factory.StartNew(() => container.GetInstance<EventNotificationsJob>().RunContinuousAsync(cancellationToken: token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default).IgnoreExceptions();
                Task.Factory.StartNew(() => container.GetInstance<WebHooksJob>().RunContinuousAsync(cancellationToken: token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default).IgnoreExceptions();
                //Task.Factory.StartNew(() => container.GetInstance<DailySummaryJob>().RunContinuousAsync(delay: TimeSpan.FromMinutes(15), cancellationToken: token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default).IgnoreExceptions();
                //Task.Factory.StartNew(() => container.GetInstance<RetentionLimitsJob>().RunContinuousAsync(delay: TimeSpan.FromHours(8), cancellationToken: token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default).IgnoreExceptions();
                //Task.Factory.StartNew(() => container.GetInstance<StaleAccountsJob>().RunContinuousAsync(delay: TimeSpan.FromHours(8), cancellationToken: token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default).IgnoreExceptions();
            }
        }

        private static void CreateSampleData(Container container) {
            if (Settings.Current.WebsiteMode != WebsiteMode.Dev)
                return;

            var userRepository = container.GetInstance<IUserRepository>();
            if (userRepository.Count() != 0)
                return;

            var dataHelper = container.GetInstance<DataHelper>();
            dataHelper.CreateTestData();
        }

        public static Container CreateContainer(bool includeInsulation = true) {
            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            container.Options.PropertySelectionBehavior = new InjectAttributePropertySelectionBehavior();

            container.RegisterPackage<Core.Bootstrapper>();
            container.RegisterPackage<Bootstrapper>();

            if (!includeInsulation)
                return container;

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

        private static void VerifyContainer(Container container) {
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
        }

        public static HttpConfiguration Config { get; private set; }
    }
}