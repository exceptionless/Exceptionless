using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Cors;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Http.ExceptionHandling;
using System.Web.Http.Routing;
using Exceptionless.Api.Hubs;
using Exceptionless.Api.Security;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Jobs.Elastic;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Serializer;
using Foundatio.Jobs;
using Foundatio.Utility;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Owin;
using SimpleInjector;
using SimpleInjector.Integration.WebApi;
using SimpleInjector.Lifestyles;
using Swashbuckle.Application;

namespace Exceptionless.Api {
    public static class AppBuilder {
        public static void Build(IAppBuilder app, Container container = null) {
            var loggerFactory = GetLoggerFactory();

            var context = new OwinContext(app.Properties);
            var shutdownCancellationToken = context.Get<CancellationToken>("host.OnAppDisposing");

            if (container == null)
                container = CreateContainer(loggerFactory, shutdownCancellationToken);

            var config = new HttpConfiguration();
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.Formatters.JsonFormatter.SerializerSettings.Formatting = Formatting.Indented;

            SetupRouteConstraints(config);
            container.AddSingleton(config);
            container.RegisterWebApiControllers(config);

            container.AddStartupAction(() => container.GetInstance<EnqueueOrganizationNotificationOnPlanOverage>().RunAsync(shutdownCancellationToken));

            if (Settings.Current.EnableSignalR)
                container.AddStartupAction(() => container.GetInstance<MessageBusBroker>().StartAsync(shutdownCancellationToken));

            if (Settings.Current.WebsiteMode == WebsiteMode.Dev)
                container.AddStartupAction(() => CreateSampleDataAsync(container));

            VerifyContainer(container);

            config.DependencyResolver = new SimpleInjectorWebApiDependencyResolver(container);

            var contractResolver = container.GetInstance<IContractResolver>();
            var dynamicTypeContractResolver = contractResolver as DynamicTypeContractResolver;
            dynamicTypeContractResolver?.UseDefaultResolverFor(typeof(Connection).Assembly);
            config.Formatters.JsonFormatter.SerializerSettings.ContractResolver = contractResolver;

            config.Services.Add(typeof(IExceptionLogger), new FoundatioExceptionLogger(loggerFactory.CreateLogger<FoundatioExceptionLogger>()));
            if (!String.IsNullOrEmpty(Settings.Current.ExceptionlessApiKey) && !String.IsNullOrEmpty(Settings.Current.ExceptionlessServerUrl))
                config.Services.Replace(typeof(IExceptionHandler), container.GetInstance<ExceptionlessReferenceIdExceptionHandler>());

            config.MessageHandlers.Add(container.GetInstance<XHttpMethodOverrideDelegatingHandler>());
            config.MessageHandlers.Add(container.GetInstance<EncodingDelegatingHandler>());
            config.MessageHandlers.Add(container.GetInstance<AuthMessageHandler>());

            // Throttle api calls to X every 15 minutes by IP address.
            config.MessageHandlers.Add(container.GetInstance<ThrottlingHandler>());

            // Reject event posts in organizations over their max event limits.
            config.MessageHandlers.Add(container.GetInstance<OverageHandler>());

            EnableCors(config, app);
            app.UseWebApi(config);
            SetupSignalR(app, container, loggerFactory);
            SetupSwagger(config);

            container.RunStartupActionsAsync().GetAwaiter().GetResult();

            var logger = loggerFactory.CreateLogger(nameof(AppBuilder));
            RunJobs(container, loggerFactory, logger, shutdownCancellationToken);
            logger.LogInformation("Starting api...");
        }

        private static void RunJobs(Container container, ILoggerFactory loggerFactory, ILogger logger, CancellationToken token) {
            if (!Settings.Current.RunJobsInProcess) {
                logger.LogInformation("Jobs running out of process.");
                return;
            }

            new JobRunner(container.GetInstance<EventPostsJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(2)).RunInBackground(token);
            new JobRunner(container.GetInstance<EventUserDescriptionsJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(3)).RunInBackground(token);
            new JobRunner(container.GetInstance<EventNotificationsJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(5)).RunInBackground(token);
            new JobRunner(container.GetInstance<MailMessageJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(5)).RunInBackground(token);
            new JobRunner(container.GetInstance<WebHooksJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(5)).RunInBackground(token);
            new JobRunner(container.GetInstance<CloseInactiveSessionsJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(30), interval: TimeSpan.FromSeconds(30)).RunInBackground(token);
            new JobRunner(container.GetInstance<DailySummaryJob>(), loggerFactory, initialDelay: TimeSpan.FromMinutes(1), interval: TimeSpan.FromHours(1)).RunInBackground(token);
            new JobRunner(container.GetInstance<DownloadGeoIPDatabaseJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(5), interval: TimeSpan.FromDays(1)).RunInBackground(token);
            new JobRunner(container.GetInstance<RetentionLimitsJob>(), loggerFactory, initialDelay: TimeSpan.FromMinutes(15), interval: TimeSpan.FromHours(1)).RunInBackground(token);
            new JobRunner(container.GetInstance<WorkItemJob>(), loggerFactory, initialDelay: TimeSpan.FromSeconds(2), instanceCount: 2).RunInBackground(token);
            new JobRunner(container.GetInstance<MaintainIndexesJob>(), loggerFactory, initialDelay: SystemClock.UtcNow.Ceiling(TimeSpan.FromHours(1)) - SystemClock.UtcNow, interval: TimeSpan.FromHours(1)).RunInBackground(token);

            logger.LogWarning("Jobs running in process.");
        }

        private static void EnableCors(HttpConfiguration config, IAppBuilder app) {
            var exposedHeaders = new List<string> { "ETag", "Link", ExceptionlessHeaders.RateLimit, ExceptionlessHeaders.RateLimitRemaining, ExceptionlessHeaders.Client, ExceptionlessHeaders.ConfigurationVersion };
            app.UseCors(new CorsOptions {
                PolicyProvider = new CorsPolicyProvider {
                    PolicyResolver = context => {
                        var policy = new CorsPolicy {
                            AllowAnyHeader = true,
                            AllowAnyMethod = true,
                            AllowAnyOrigin = true,
                            SupportsCredentials = true,
                            PreflightMaxAge = 60 * 5
                        };

                        policy.ExposedHeaders.AddRange(exposedHeaders);
                        return Task.FromResult(policy);
                    }
                }
            });

            var enableCorsAttribute = new EnableCorsAttribute("*", "*", "*") {
                SupportsCredentials = true,
                PreflightMaxAge = 60 * 5
            };

            enableCorsAttribute.ExposedHeaders.AddRange(exposedHeaders);
            config.EnableCors(enableCorsAttribute);
        }

        private static void SetupRouteConstraints(HttpConfiguration config) {
            var constraintResolver = new DefaultInlineConstraintResolver();
            constraintResolver.ConstraintMap.Add("identifier", typeof(IdentifierRouteConstraint));
            constraintResolver.ConstraintMap.Add("identifiers", typeof(IdentifiersRouteConstraint));
            constraintResolver.ConstraintMap.Add("objectid", typeof(ObjectIdRouteConstraint));
            constraintResolver.ConstraintMap.Add("objectids", typeof(ObjectIdsRouteConstraint));
            constraintResolver.ConstraintMap.Add("token", typeof(TokenRouteConstraint));
            constraintResolver.ConstraintMap.Add("tokens", typeof(TokensRouteConstraint));
            config.MapHttpAttributeRoutes(constraintResolver);
        }

        private static void SetupSignalR(IAppBuilder app, Container container, ILoggerFactory loggerFactory) {
            if (!Settings.Current.EnableSignalR)
                return;

            var resolver = container.GetInstance<IDependencyResolver>();
            var hubPipeline = (IHubPipeline)resolver.GetService(typeof(IHubPipeline));
            hubPipeline.AddModule(new ErrorHandlingPipelineModule(loggerFactory.CreateLogger<ErrorHandlingPipelineModule>()));

            app.MapSignalR<MessageBusConnection>("/api/v2/push", new ConnectionConfiguration { Resolver = resolver });
        }

        private static void SetupSwagger(HttpConfiguration config) {
            config.EnableSwagger("schema/{apiVersion}", c => {
                c.SingleApiVersion("v2", "Exceptionless");
                c.ApiKey("access_token").In("header").Name("access_token").Description("API Key Authentication");
                c.BasicAuth("basic").Description("Basic HTTP Authentication");
                c.IncludeXmlComments($@"{AppDomain.CurrentDomain.BaseDirectory}\bin\Exceptionless.Api.xml");
                c.IgnoreObsoleteActions();
                c.DocumentFilter<FilterRoutesDocumentFilter>();
            }).EnableSwaggerUi("docs/{*assetPath}", c => {
                c.InjectStylesheet(typeof(AppBuilder).Assembly, "Exceptionless.Api.Content.docs.css");
                c.InjectJavaScript(typeof(AppBuilder).Assembly, "Exceptionless.Api.Content.docs.js");
            });
        }

        private static async Task CreateSampleDataAsync(Container container) {
            if (Settings.Current.WebsiteMode != WebsiteMode.Dev)
                return;

            var userRepository = container.GetInstance<IUserRepository>();
            if (await userRepository.CountAsync() != 0)
                return;

            var dataHelper = container.GetInstance<SampleDataService>();
            await dataHelper.CreateDataAsync();
        }

        private static ILoggerFactory GetLoggerFactory() {
            var loggerFactory = Settings.Current.GetLoggerFactory();
            Insulation.Bootstrapper.ConfigureLoggerFactory(loggerFactory);
            return loggerFactory;
        }

        public static Container CreateContainer(ILoggerFactory loggerFactory, CancellationToken shutdownCancellationToken) {
            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            Core.Bootstrapper.RegisterServices(container, loggerFactory, shutdownCancellationToken);
            Bootstrapper.RegisterServices(container, loggerFactory, shutdownCancellationToken);
            Insulation.Bootstrapper.RegisterServices(container, Settings.Current.RunJobsInProcess, loggerFactory, shutdownCancellationToken);

            return container;
        }

        [Conditional("DEBUG")]
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

                if (tempEx is ReflectionTypeLoadException typeLoadException) {
                    foreach (var loaderEx in typeLoadException.LoaderExceptions)
                        Debug.WriteLine(loaderEx.Message);
                }

                Debug.WriteLine(ex.Message);
                throw;
            }
        }
    }
}