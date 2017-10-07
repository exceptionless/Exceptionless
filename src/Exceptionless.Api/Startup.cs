using System;
using System.IO;
using Exceptionless.Api.Hubs;
using Exceptionless.Api.Security;
using Exceptionless.Api.Utility;
using Exceptionless.Api.Utility.Handlers;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Swagger;

namespace Exceptionless.Api {
    public class Startup {
        private readonly ILoggerFactory _loggerFactory;

        public Startup(ILoggerFactory loggerFactory) {
            _loggerFactory = loggerFactory;
        }

        public void Configure(IApplicationBuilder app) {
            if (!String.IsNullOrEmpty(Settings.Current.ExceptionlessApiKey) && !String.IsNullOrEmpty(Settings.Current.ExceptionlessServerUrl))
                app.UseExceptionless(ExceptionlessClient.Default);

            app.UseCors(c => c
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowAnyOrigin()
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(5))
                .WithExposedHeaders("ETag", "Link", "X-RateLimit-Limit", "X-RateLimit-Remaining", "X-Result-Count"));

            //config.MessageHandlers.Add(container.GetInstance<XHttpMethodOverrideDelegatingHandler>());
            //config.MessageHandlers.Add(container.GetInstance<EncodingDelegatingHandler>());
            app.UseHttpMethodOverride();
            app.UseForwardedHeaders();
            app.UseMiddleware<ApiKeyMiddleware>();
            // Reject event posts in organizations over their max event limits.
            app.UseMiddleware<OverageMiddleware>();
            // Throttle api calls to X every 15 minutes by IP address.
            app.UseMiddleware<ThrottlingMiddleware>();
            app.UseFileServer();
            app.UseMvc();
            app.UseSwagger(c => {
                c.RouteTemplate = "docs/{documentName}/swagger.json";
            });
            app.UseSwaggerUI(s => {
                s.RoutePrefix = "docs";
                s.SwaggerEndpoint("/docs/v2/swagger.json", "Exceptionless API");
                s.InjectStylesheet("docs.css");
                s.InjectOnCompleteJavaScript("docs.js");
            });

            app.UseWebSockets();
            app.UseMiddleware<MessageBusBrokerMiddleware>();

            // run startup actions registered in the container
            if (!Settings.Current.DisableBootstrapStartupActions) {
                var lifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();
                lifetime.ApplicationStarted.Register(() => app.ApplicationServices.RunStartupActionsAsync(lifetime.ApplicationStopping).GetAwaiter().GetResult());
            }
        }

        public void ConfigureServices(IServiceCollection services) {
            ConfigureServicesInternal(services);
        }

        public void ConfigureProductionServices(IServiceCollection services) {
            ConfigureServicesInternal(services, true);
        }

        private void ConfigureServicesInternal(IServiceCollection services, bool includeInsulation = false) {
            services.AddCors();
            services.Configure<ForwardedHeadersOptions>(options => {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.RequireHeaderSymmetry = false;
            });
            services.AddMvc(o => {
                o.Filters.Add<RequireHttpsExceptLocalAttribute>();
                o.Filters.Add<ApiExceptionFilter>();
                o.ModelBinderProviders.Add(new CustomAttributesModelBinderProvider());
                o.InputFormatters.Insert(0, new RawRequestBodyFormatter());
            }).AddJsonOptions(o => {
                o.SerializerSettings.DefaultValueHandling = DefaultValueHandling.Include;
                o.SerializerSettings.NullValueHandling = NullValueHandling.Include;
                o.SerializerSettings.Formatting = Formatting.Indented;
                o.SerializerSettings.ContractResolver = Core.Bootstrapper.GetJsonContractResolver(); // TODO: See if we can resolve this from the di.
            });

            services.AddRouting(r => {
                r.LowercaseUrls = true;
                r.ConstraintMap.Add("identifier", typeof(IdentifierRouteConstraint));
                r.ConstraintMap.Add("identifiers", typeof(IdentifiersRouteConstraint));
                r.ConstraintMap.Add("objectid", typeof(ObjectIdRouteConstraint));
                r.ConstraintMap.Add("objectids", typeof(ObjectIdsRouteConstraint));
                r.ConstraintMap.Add("token", typeof(TokenRouteConstraint));
                r.ConstraintMap.Add("tokens", typeof(TokensRouteConstraint));
            });
            services.AddSwaggerGen(c => {
                c.SwaggerDoc("v2", new Info {
                    Title = "Exceptionless API",
                    Version = "v2"
                });
                c.AddSecurityDefinition("access_token", new ApiKeyScheme {
                    Name = "access_token",
                    In = "header",
                    Description = "API Key Authentication"
                });
                c.AddSecurityDefinition("basic", new BasicAuthScheme {
                    Description = "Basic HTTP Authentication"
                });
                if (File.Exists($@"{AppDomain.CurrentDomain.BaseDirectory}\bin\Exceptionless.Api.xml"))
                    c.IncludeXmlComments($@"{AppDomain.CurrentDomain.BaseDirectory}\bin\Exceptionless.Api.xml");
                c.IgnoreObsoleteActions();
            });

            Bootstrapper.RegisterServices(services, _loggerFactory, includeInsulation);

            services.AddSingleton(new ThrottlingOptions {
                MaxRequestsForUserIdentifierFunc = userIdentifier => Settings.Current.ApiThrottleLimit,
                Period = TimeSpan.FromMinutes(15)
            });
        }
    }
}