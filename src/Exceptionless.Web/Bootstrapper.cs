using System;
using System.Threading.Tasks;
using AutoMapper;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Web.Hubs;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.Handlers;
using Foundatio.Hosting.Startup;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Token = Exceptionless.Core.Models.Token;

namespace Exceptionless.Web {
    public class Bootstrapper {
        public static void RegisterServices(IServiceCollection services, ILoggerFactory loggerFactory) {
            services.AddSingleton<WebSocketConnectionManager>();
            services.AddSingleton<MessageBusBroker>();
            services.AddSingleton<MessageBusBrokerMiddleware>();

            services.AddSingleton<OverageMiddleware>();
            services.AddSingleton<ThrottlingMiddleware>();

            services.AddTransient<Profile, ApiMappings>();

            Core.Bootstrapper.RegisterServices(services);
            
            var serviceProvider = services.BuildServiceProvider();
            var options = serviceProvider.GetRequiredService<IOptions<AppOptions>>().Value;
            Insulation.Bootstrapper.RegisterServices(serviceProvider, services, options, options.RunJobsInProcess);

            if (options.RunJobsInProcess)
                Core.Bootstrapper.AddHostedJobs(services, loggerFactory);

            var logger = loggerFactory.CreateLogger<Startup>();
            services.AddStartupAction<MessageBusBroker>();
            services.AddStartupAction("Subscribe to Log Work Item Progress", (sp, ct) => {
                var subscriber = sp.GetRequiredService<IMessageSubscriber>();
                return subscriber.SubscribeAsync<WorkItemStatus>(workItemStatus => {
                    if (logger.IsEnabled(LogLevel.Trace))
                        logger.LogTrace("WorkItem id:{WorkItemId} message:{Message} progress:{Progress}", workItemStatus.WorkItemId ?? "<NULL>", workItemStatus.Message ?? "<NULL>", workItemStatus.Progress);

                    return Task.CompletedTask;
                }, ct);
            });

            services.AddSingleton<EnqueueOrganizationNotificationOnPlanOverage>();
            services.AddStartupAction<EnqueueOrganizationNotificationOnPlanOverage>();
        }

        public class ApiMappings : Profile {
            public ApiMappings(BillingPlans plans) {
                CreateMap<UserDescription, EventUserDescription>();

                CreateMap<NewOrganization, Organization>();
                CreateMap<Organization, ViewOrganization>().AfterMap((o, vo) => {
                    vo.IsOverHourlyLimit = o.IsOverHourlyLimit(plans);
                    vo.IsOverMonthlyLimit = o.IsOverMonthlyLimit();
                });

                CreateMap<Stripe.Invoice, InvoiceGridModel>().AfterMap((si, igm) => {
                   igm.Id = igm.Id.Substring(3);
                   igm.Date = si.Created;
                });

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