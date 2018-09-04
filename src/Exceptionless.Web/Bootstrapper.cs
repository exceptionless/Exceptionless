using System;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.Formatters.Prometheus;
using App.Metrics.Formatters;
using AutoMapper;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Insulation.Metrics;
using Exceptionless.Web.Hubs;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.Handlers;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Exceptionless.Web {
    public class Bootstrapper {
        public static void ConfigureWebHost(IWebHostBuilder builder) {
            if (!String.IsNullOrEmpty(Settings.Current.ApplicationInsightsKey))
                builder.UseApplicationInsights(Settings.Current.ApplicationInsightsKey);

            // Note: The prometheus reports metrics in passive mode, so it should only used in webapps but not in console apps.
            if (Settings.Current.EnableMetricsReporting) {
                if (Settings.Current.MetricsConnectionString is PrometheusMetricsConnectionString) {
                    var metrics = AppMetrics.CreateDefaultBuilder()
                        .OutputMetrics.AsPrometheusPlainText()
                        .OutputMetrics.AsPrometheusProtobuf()
                        .Build();
                    builder.ConfigureMetrics(metrics).UseMetrics(options => {
                        options.EndpointOptions = endpointsOptions => {
                            endpointsOptions.MetricsTextEndpointOutputFormatter = metrics.OutputMetricsFormatters.GetType<MetricsPrometheusTextOutputFormatter>();
                            endpointsOptions.MetricsEndpointOutputFormatter = metrics.OutputMetricsFormatters.GetType<MetricsPrometheusProtobufOutputFormatter>();
                        };
                    });
                }
                else if (!(Settings.Current.MetricsConnectionString is StatsDMetricsConnectionString)) {
                    builder.UseMetrics();
                }
            }
        }

        public static void RegisterServices(IServiceCollection container, ILoggerFactory loggerFactory) {
            container.AddSingleton<WebSocketConnectionManager>();
            container.AddSingleton<MessageBusBroker>();
            container.AddSingleton<MessageBusBrokerMiddleware>();

            container.AddSingleton<OverageMiddleware>();
            container.AddSingleton<ThrottlingMiddleware>();

            container.AddTransient<Profile, ApiMappings>();

            Core.Bootstrapper.RegisterServices(container);
            bool includeInsulation = !String.IsNullOrEmpty(Settings.Current.RedisConnectionString) ||
                !String.IsNullOrEmpty(Settings.Current.AzureStorageConnectionString) ||
                !String.IsNullOrEmpty(Settings.Current.AzureStorageQueueConnectionString) ||
                !String.IsNullOrEmpty(Settings.Current.AliyunStorageConnectionString) ||
                !String.IsNullOrEmpty(Settings.Current.MinioStorageConnectionString) ||
                Settings.Current.EnableMetricsReporting;
            if (includeInsulation)
                Insulation.Bootstrapper.RegisterServices(container, Settings.Current.RunJobsInProcess);

            if (Settings.Current.RunJobsInProcess)
                container.AddSingleton<IHostedService, JobsHostedService>();

            var logger = loggerFactory.CreateLogger<Startup>();
            container.AddStartupAction<MessageBusBroker>();
            container.AddStartupAction((sp, ct) => {
                var subscriber = sp.GetRequiredService<IMessageSubscriber>();
                return subscriber.SubscribeAsync<WorkItemStatus>(workItemStatus => {
                    if (logger.IsEnabled(LogLevel.Trace))
                        logger.LogTrace("WorkItem id:{WorkItemId} message:{Message} progress:{Progress}", workItemStatus.WorkItemId ?? "<NULL>", workItemStatus.Message ?? "<NULL>", workItemStatus.Progress);

                    return Task.CompletedTask;
                }, ct);
            });

            container.AddSingleton<EnqueueOrganizationNotificationOnPlanOverage>();
            container.AddStartupAction<EnqueueOrganizationNotificationOnPlanOverage>();
        }

        public class ApiMappings : Profile {
            public ApiMappings() {
                CreateMap<UserDescription, EventUserDescription>();

                CreateMap<NewOrganization, Organization>();
                CreateMap<Organization, ViewOrganization>().AfterMap((o, vo) => {
                    vo.IsOverHourlyLimit = o.IsOverHourlyLimit();
                    vo.IsOverMonthlyLimit = o.IsOverMonthlyLimit();
                });

                CreateMap<StripeInvoice, InvoiceGridModel>().AfterMap((si, igm) => igm.Id = igm.Id.Substring(3));

                CreateMap<NewProject, Project>();
                CreateMap<Project, ViewProject>().AfterMap((p, vp) => vp.HasSlackIntegration = p.Data.ContainsKey(Project.KnownDataKeys.SlackToken));

                CreateMap<NewToken, Token>().ForMember(m => m.Type, m => m.Ignore());
                CreateMap<Token, ViewToken>();

                CreateMap<User, ViewUser>();

                CreateMap<NewWebHook, WebHook>();
            }
        }
    }
}