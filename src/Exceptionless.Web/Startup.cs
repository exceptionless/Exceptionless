using System;
using System.Linq;
using System.IO;
using System.Security.Claims;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Web.Hubs;
using Exceptionless.Web.Security;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Joonasw.AspNetCore.SecurityHeaders;
using System.Collections.Generic;
using Exceptionless.Web.Extensions;
using Foundatio.Extensions.Hosting.Startup;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Exceptionless.Web {
    public class Startup {
        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services) {
            services.AddCors(b => b.AddPolicy("AllowAny", p => p
                .AllowAnyHeader()
                .AllowAnyMethod()
                .SetIsOriginAllowed(isOriginAllowed: _ => true)
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(5))
                .WithExposedHeaders("ETag", "Link", Headers.RateLimit, Headers.RateLimitRemaining, "X-Result-Count", Headers.LegacyConfigurationVersion, Headers.ConfigurationVersion)));

            services.Configure<ForwardedHeadersOptions>(options => {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.RequireHeaderSymmetry = false;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            services.AddControllers(o => {
                o.Filters.Add<ApiExceptionFilter>();
                o.ModelBinderProviders.Insert(0, new CustomAttributesModelBinderProvider());
                o.InputFormatters.Insert(0, new RawRequestBodyFormatter());
            }).AddNewtonsoftJson(o => {
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
                c.SwaggerDoc("v2", new OpenApiInfo {
                    Title = "Exceptionless API",
                    Version = "v2"
                });

                c.AddSecurityDefinition("Basic", new OpenApiSecurityScheme {
                    Description = "Basic HTTP Authentication",
                    Scheme = "basic",
                    Type = SecuritySchemeType.Http
                });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
                    Description = "Authorization token. Example: \"Bearer {apikey}\"",
                    Scheme = "bearer",
                    Type = SecuritySchemeType.Http
                });
                c.AddSecurityDefinition("Token", new OpenApiSecurityScheme {
                    Description = "Authorization token. Example: \"Bearer {apikey}\"",
                    Name = "access_token",
                    In = ParameterLocation.Query,
                    Type = SecuritySchemeType.ApiKey
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement {
                    {
                        new OpenApiSecurityScheme {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Basic" }
                        },
                        Array.Empty<string>()
                    },
                    {
                        new OpenApiSecurityScheme {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        Array.Empty<string>()
                    },
                    {
                        new OpenApiSecurityScheme {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Token" }
                        },
                        Array.Empty<string>()
                    }
                });

                string xmlDocPath = Path.Combine(AppContext.BaseDirectory, "Exceptionless.Web.xml");
                if (File.Exists(xmlDocPath))
                    c.IncludeXmlComments(xmlDocPath);

                c.IgnoreObsoleteActions();
                c.OperationFilter<RequestBodyOperationFilter>();
            });

            var appOptions = AppOptions.ReadFromConfiguration(Configuration);
            Bootstrapper.RegisterServices(services, appOptions, Log.Logger.ToLoggerFactory());
            services.AddSingleton(s => {
                return new ThrottlingOptions {
                    MaxRequestsForUserIdentifierFunc = userIdentifier => appOptions.ApiThrottleLimit,
                    Period = TimeSpan.FromMinutes(15)
                };
            });
        }

        public void Configure(IApplicationBuilder app) {
            var options = app.ApplicationServices.GetRequiredService<AppOptions>();
            Core.Bootstrapper.LogConfiguration(app.ApplicationServices, options, Log.Logger.ToLoggerFactory().CreateLogger<Startup>());

            app.UseMiddleware<AllowSynchronousIOMiddleware>();

            app.UseHealthChecks("/health", new HealthCheckOptions {
                Predicate = hcr => hcr.Tags.Contains("Critical") || (options.RunJobsInProcess && hcr.Tags.Contains("AllJobs"))
            });

            var readyTags = new List<string> { "Critical" };
            if (!options.EventSubmissionDisabled)
                readyTags.Add("Storage");
            app.UseReadyHealthChecks(readyTags.ToArray());
            app.UseWaitForStartupActionsBeforeServingRequests();

            if (!String.IsNullOrEmpty(options.ExceptionlessApiKey) && !String.IsNullOrEmpty(options.ExceptionlessServerUrl))
                app.UseExceptionless(ExceptionlessClient.Default);

            app.UseCsp(csp => {
                csp.ByDefaultAllow.FromSelf()
                    .From("https://js.stripe.com")
                    .From("http://js.stripe.com");
                csp.AllowFonts.FromSelf()
                    .From("https://fonts.gstatic.com")
                    .From("http://fonts.gstatic.com")
                    .From("https://cdn.jsdelivr.net")
                    .From("http://cdn.jsdelivr.net");
                csp.AllowImages.FromSelf()
                    .From("data:")
                    .From("https://q.stripe.com")
                    .From("http://q.stripe.com")
                    .From("https://www.gravatar.com")
                    .From("http://www.gravatar.com");
                csp.AllowScripts.FromSelf()
                    .AllowUnsafeInline()
                    .AllowUnsafeEval()
                    .From("https://js.stripe.com")
                    .From("http://js.stripe.com")
                    .From("https://cdn.jsdelivr.net")
                    .From("http://cdn.jsdelivr.net");
                csp.AllowStyles.FromSelf()
                    .AllowUnsafeInline()
                    .From("https://fonts.googleapis.com")
                    .From("http://fonts.googleapis.com")
                    .From("https://cdn.jsdelivr.net")
                    .From("http://cdn.jsdelivr.net");
            });

            app.Use(async (context, next) => {
                if (options.AppMode != AppMode.Development && context.Request.IsLocal() == false)
                    context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");

                context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Add("X-Frame-Options", "DENY");
                context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
                context.Response.Headers.Remove("X-Powered-By");

                await next();
            });

            var serverAddressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();
            if (options.AppMode != AppMode.Development && serverAddressesFeature != null && serverAddressesFeature.Addresses.Any(a => a.StartsWith("https://")))
                app.UseHttpsRedirection();

            app.UseSerilogRequestLogging(o => o.GetLevel = (context, duration, ex) => {
                if (ex != null || context.Response.StatusCode > 499)
                    return LogEventLevel.Error;

                if (context.Response.StatusCode > 399)
                    return LogEventLevel.Information;

                if (duration < 1000 || context.Request.Path.StartsWithSegments("/api/v2/push"))
                    return LogEventLevel.Debug;

                return LogEventLevel.Information;
            });
            app.UseStaticFiles(new StaticFileOptions {
                ContentTypeProvider = new FileExtensionContentTypeProvider {
                    Mappings = {
                        [".less"] = "plain/text"
                    }
                }
            });

            app.UseDefaultFiles();
            app.UseFileServer();
            app.UseRouting();
            app.UseCors("AllowAny");
            app.UseHttpMethodOverride();
            app.UseForwardedHeaders();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseMiddleware<ProjectConfigMiddleware>();
            app.UseMiddleware<RecordSessionHeartbeatMiddleware>();

            if (options.ApiThrottleLimit < Int32.MaxValue) {
                // Throttle api calls to X every 15 minutes by IP address.
                app.UseMiddleware<ThrottlingMiddleware>();
            }

            // Reject event posts in organizations over their max event limits.
            app.UseMiddleware<OverageMiddleware>();

            app.UseSwagger(c => {
                c.RouteTemplate = "docs/{documentName}/swagger.json";
                // TODO: Remove once 5.6.4+ is released 
                c.PreSerializeFilters.Add((doc, _) => doc.Servers?.Clear());
            });
            app.UseSwaggerUI(s => {
                s.RoutePrefix = "docs";
                s.SwaggerEndpoint("/docs/v2/swagger.json", "Exceptionless API");
                s.InjectStylesheet("/docs.css");
            });

            if (options.EnableWebSockets) {
                app.UseWebSockets();
                app.UseMiddleware<MessageBusBrokerMiddleware>();
            }

            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();
                endpoints.MapFallbackToFile("{**slug:nonfile}", "index.html");
            });
        }
    }
}
