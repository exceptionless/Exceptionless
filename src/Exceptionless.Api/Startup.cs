using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Hubs;
using Exceptionless.Api.Security;
using Exceptionless.Api.Utility;
using Exceptionless.Api.Utility.Handlers;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Cors.Internal;
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
            Core.Bootstrapper.LogConfiguration(app.ApplicationServices, _loggerFactory);

            if (!String.IsNullOrEmpty(Settings.Current.ExceptionlessApiKey) && !String.IsNullOrEmpty(Settings.Current.ExceptionlessServerUrl))
                app.UseExceptionless(ExceptionlessClient.Default);

            app.UseCors("AllowAny");
            app.UseHttpMethodOverride();
            app.UseForwardedHeaders();
            app.UseAuthentication();
            app.UseMiddleware<ProjectConfigMiddleware>();
            app.UseMiddleware<RecordSessionHeartbeatMiddleware>();

            if (Settings.Current.ApiThrottleLimit < Int32.MaxValue) {
                // Throttle api calls to X every 15 minutes by IP address.
                app.UseMiddleware<ThrottlingMiddleware>();
            }

            // Reject event posts in organizations over their max event limits.
            app.UseMiddleware<OverageMiddleware>();
            app.UseFileServer();
            app.UseMvc();
            app.UseSwagger(c => {
                c.RouteTemplate = "docs/{documentName}/swagger.json";
            });
            app.UseSwaggerUI(s => {
                s.RoutePrefix = "docs";
                s.SwaggerEndpoint("/docs/v2/swagger.json", "Exceptionless API V2");
                s.SwaggerEndpoint("/docs/v1/swagger.json", "Exceptionless API V1");
                s.InjectStylesheet("/docs.css");
            });

            if (Settings.Current.EnableWebSockets) {
                app.UseWebSockets();
                app.UseMiddleware<MessageBusBrokerMiddleware>();
            }

            // run startup actions registered in the container
            if (Settings.Current.EnableBootstrapStartupActions) {
                var lifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();
                lifetime.ApplicationStarted.Register(() => {
                    var shutdownSource = new CancellationTokenSource();
                    Console.CancelKeyPress += (sender, args) => {
                        shutdownSource.Cancel();
                        args.Cancel = true;
                    };

                    var combined = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping, shutdownSource.Token);
                    app.ApplicationServices.RunStartupActionsAsync(combined.Token).GetAwaiter().GetResult();
                });
            }
        }

        public void ConfigureServices(IServiceCollection services) {
            services.AddCors(b => b.AddPolicy("AllowAny", p => p
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowAnyOrigin()
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(5))
                .WithExposedHeaders("ETag", "Link", "X-RateLimit-Limit", "X-RateLimit-Remaining", "X-Result-Count")));

            services.Configure<ForwardedHeadersOptions>(options => {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.RequireHeaderSymmetry = false;
            });
            services.AddMvc(o => {
                o.Filters.Add(new CorsAuthorizationFilterFactory("AllowAny"));
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

            services.AddAuthentication(ApiKeyAuthenticationOptions.ApiKeySchema).AddApiKeyAuthentication();
            services.AddAuthorization(options => {
                options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                options.AddPolicy(AuthorizationRoles.ClientPolicy, policy => policy.RequireClaim(ClaimTypes.Role, AuthorizationRoles.Client));
                options.AddPolicy(AuthorizationRoles.UserPolicy, policy => policy.RequireClaim(ClaimTypes.Role, AuthorizationRoles.User));
                options.AddPolicy(AuthorizationRoles.GlobalAdminPolicy, policy => policy.RequireClaim(ClaimTypes.Role, AuthorizationRoles.GlobalAdmin));
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
                    Title = "Exceptionless API V2",
                    Version = "v2"
                });
                c.SwaggerDoc("v1", new Info {
                    Title = "Exceptionless API V1",
                    Version = "v1"
                });

                c.AddSecurityDefinition("access_token", new ApiKeyScheme {
                    Name = "access_token",
                    In = "header",
                    Description = "API Key Authentication"
                });
                c.AddSecurityDefinition("basic", new BasicAuthScheme {
                    Description = "Basic HTTP Authentication"
                });
                if (File.Exists($@"{AppDomain.CurrentDomain.BaseDirectory}\Exceptionless.Api.xml"))
                    c.IncludeXmlComments($@"{AppDomain.CurrentDomain.BaseDirectory}\Exceptionless.Api.xml");
                c.IgnoreObsoleteActions();
                c.AddAutoVersioningSupport();
            });

            Bootstrapper.RegisterServices(services, _loggerFactory);

            services.AddSingleton(new ThrottlingOptions {
                MaxRequestsForUserIdentifierFunc = userIdentifier => Settings.Current.ApiThrottleLimit,
                Period = TimeSpan.FromMinutes(15)
            });
        }
    }
}