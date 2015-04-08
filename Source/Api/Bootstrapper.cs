using System;
using Exceptionless.Api.Hubs;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Serializer;
using Foundatio.Caching;
using Foundatio.Serializer;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SimpleInjector;
using SimpleInjector.Packaging;
using PrincipalUserIdProvider = Exceptionless.Api.Hubs.PrincipalUserIdProvider;

namespace Exceptionless.Api {
    public class Bootstrapper : IPackage {
        public void RegisterServices(Container container) {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings {
                DateParseHandling = DateParseHandling.DateTimeOffset
            };

            var contractResolver = new ExceptionlessContractResolver();
            contractResolver.UseDefaultResolverFor(typeof(Connection).Assembly);
            contractResolver.UseDefaultResolverFor(typeof(DataDictionary), typeof(SettingsDictionary), typeof(VersionOne.VersionOneWebHookStack), typeof(VersionOne.VersionOneWebHookEvent));

            var settings = new JsonSerializerSettings {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                DateParseHandling = DateParseHandling.DateTimeOffset,
                ContractResolver = contractResolver
            };
            settings.AddModelConverters();

            container.RegisterSingle<IContractResolver>(() => contractResolver);
            container.RegisterSingle<JsonSerializerSettings>(settings);
            container.RegisterSingle<JsonSerializer>(JsonSerializer.Create(settings));
            container.RegisterSingle<ISerializer>(() => new JsonNetSerializer(settings));

            container.RegisterSingle<IUserIdProvider, PrincipalUserIdProvider>();
            container.RegisterSingle<MessageBusHub>();
            container.Register<OverageHandler>();
            container.Register<ThrottlingHandler>(() => new ThrottlingHandler(container.GetInstance<ICacheClient>(), userIdentifier => Settings.Current.ApiThrottleLimit, TimeSpan.FromMinutes(15)));
        }
    }
}