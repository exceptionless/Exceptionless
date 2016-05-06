using System;
using AutoMapper;
using Exceptionless.Api.Hubs;
using Exceptionless.Api.Models;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;
using Foundatio.Caching;
using Foundatio.Logging;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using SimpleInjector;
using SimpleInjector.Advanced;
using Stripe;
using PrincipalUserIdProvider = Exceptionless.Api.Hubs.PrincipalUserIdProvider;

namespace Exceptionless.Api {
    public class Bootstrapper {
        public static void RegisterServices(Container container, ILoggerFactory loggerFactory) {
            container.Register<IUserIdProvider, PrincipalUserIdProvider>();
            container.Register<MessageBusConnection>();
            container.RegisterSingleton<IConnectionMapping, ConnectionMapping>();
            container.RegisterSingleton<MessageBusBroker>();

            var resolver = new SimpleInjectorSignalRDependencyResolver(container);
            container.RegisterSingleton<IDependencyResolver>(resolver);
            container.RegisterSingleton<IConnectionManager>(() => new ConnectionManager(resolver));

            container.RegisterSingleton<OverageHandler>();
            container.RegisterSingleton<ThrottlingHandler>(() => new ThrottlingHandler(container.GetInstance<ICacheClient>(), userIdentifier => Settings.Current.ApiThrottleLimit, TimeSpan.FromMinutes(15)));
            
            container.AppendToCollection(typeof(Profile), typeof(ApiMappings));
        }

        public class ApiMappings : Profile {
            protected override void Configure() {
                CreateMap<UserDescription, EventUserDescription>();
                
                CreateMap<NewOrganization, Organization>();
                CreateMap<Organization, ViewOrganization>().AfterMap((o, vo) => {
                    vo.IsOverHourlyLimit = o.IsOverHourlyLimit();
                    vo.IsOverMonthlyLimit = o.IsOverMonthlyLimit();
                });

                CreateMap<StripeInvoice, InvoiceGridModel>().AfterMap((si, igm) => igm.Id = igm.Id.Substring(3));

                CreateMap<NewProject, Project>();
                CreateMap<Project, ViewProject>();

                CreateMap<NewToken, Token>().ForMember(m => m.Type, m => m.Ignore());
                CreateMap<Token, ViewToken>();
                
                CreateMap<User, ViewUser>();

                CreateMap<NewWebHook, WebHook>();
            }
        }
    }
}