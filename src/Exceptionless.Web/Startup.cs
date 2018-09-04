using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using Exceptionless.Web.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Web.Hubs;
using Exceptionless.Web.Security;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Cors.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Swagger;
using Joonasw.AspNetCore.SecurityHeaders;
using System.Collections.Generic;

namespace Exceptionless.Web {
    public class Startup {
        public Startup(ILoggerFactory loggerFactory) {
            LoggerFactory = loggerFactory;
        }

        public ILoggerFactory LoggerFactory { get; }

        public void ConfigureServices(IServiceCollection services) {
            services.AddCors(b => b.AddPolicy("AllowAny", p => p
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowAnyOrigin()
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(5))
                .WithExposedHeaders("ETag", "Link", Headers.RateLimit, Headers.RateLimitRemaining, "X-Result-Count", Headers.LegacyConfigurationVersion, Headers.ConfigurationVersion)));

            services.Configure<ForwardedHeadersOptions>(options => {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.RequireHeaderSymmetry = false;
            });
            services.AddMvc(o => {
                o.Filters.Add(new CorsAuthorizationFilterFactory("AllowAny"));
                o.Filters.Add<RequireHttpsExceptLocalAttribute>();
                o.Filters.Add<ApiExceptionFilter>();
                o.ModelBinderProviders.Insert(0, new CustomAttributesModelBinderProvider());
                o.InputFormatters.Insert(0, new RawRequestBodyFormatter());
            }).SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
              .AddJsonOptions(o => {
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
                    Title = "Exceptionless API",
                    Version = "v2"
                });

                c.AddSecurityDefinition("Bearer", new ApiKeyScheme {
                    Description = "Authorization token. Example: \"Bearer {apikey}\"",
                    Name = "Authorization",
                    In = "header",
                    Type = "apiKey",
                });
                c.AddSecurityDefinition("Basic", new BasicAuthScheme {
                    Type = "basic",
                    Description = "Basic HTTP Authentication"
                });
                c.AddSecurityRequirement(new Dictionary<string, IEnumerable<string>> {
                    { "Basic", new string[] { } },
                    { "Bearer", new string[] { } }
                });
                
                c.OperationFilter<ExceptionlessOperationFilter>();

                if (File.Exists($@"{AppDomain.CurrentDomain.BaseDirectory}\Exceptionless.Web.xml"))
                    c.IncludeXmlComments($@"{AppDomain.CurrentDomain.BaseDirectory}\Exceptionless.Web.xml");
                
                c.IgnoreObsoleteActions();
            });

            var serviceProvider = services.BuildServiceProvider();
            var settings = serviceProvider.GetRequiredService<Settings>();
            Bootstrapper.RegisterServices(services, LoggerFactory);

            services.AddSingleton(new ThrottlingOptions {
                MaxRequestsForUserIdentifierFunc = userIdentifier => settings.ApiThrottleLimit,
                Period = TimeSpan.FromMinutes(15)
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env) {
            var settings = app.ApplicationServices.GetRequiredService<Settings>();
            Core.Bootstrapper.LogConfiguration(app.ApplicationServices, settings, LoggerFactory);

            if (!String.IsNullOrEmpty(settings.ExceptionlessApiKey) && !String.IsNullOrEmpty(settings.ExceptionlessServerUrl))
                app.UseExceptionless(ExceptionlessClient.Default);

            app.UseCsp(csp => {
                csp.ByDefaultAllow.FromSelf();
                csp.AllowFonts.FromSelf()
                    .From("https://fonts.gstatic.com");
                csp.AllowImages.FromSelf()
                    .From("data:");
                csp.AllowScripts.FromSelf()
                    .AllowUnsafeInline()
                    .From("https://cdnjs.cloudflare.com")
                    .From("https://js.stripe.com")
                    .From("https://maxcdn.bootstrapcdn.com");
                csp.AllowStyles.FromSelf()
                    .AllowUnsafeInline()
                    .From("https://fonts.googleapis.com")
                    .From("https://maxcdn.bootstrapcdn.com");
            });

            app.Use(async (context, next) => {
                context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
                context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Add("X-Frame-Options", "DENY");
                context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");

                await next();
            });

            app.UseCors("AllowAny");
            app.UseHttpMethodOverride();
            app.UseForwardedHeaders();
            app.UseAuthentication();
            app.UseMiddleware<ProjectConfigMiddleware>();
            app.UseMiddleware<RecordSessionHeartbeatMiddleware>();

            if (settings.ApiThrottleLimit < Int32.MaxValue) {
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
                s.SwaggerEndpoint("/docs/v2/swagger.json", "Exceptionless API");
                s.InjectStylesheet("/docs.css");
            });

            if (settings.EnableWebSockets) {
                app.UseWebSockets();
                app.UseMiddleware<MessageBusBrokerMiddleware>();
            }

            // run startup actions registered in the container
            if (settings.EnableBootstrapStartupActions) {
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
    }
}