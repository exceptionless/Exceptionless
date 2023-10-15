using System.Diagnostics;
using System.Security.Claims;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Serialization;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Hubs;
using Exceptionless.Web.Security;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.Handlers;
using Foundatio.Extensions.Hosting.Startup;
using Joonasw.AspNetCore.SecurityHeaders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.NewtonsoftJson;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using OpenTelemetry;
using Serilog;
using Serilog.Events;
using Unchase.Swashbuckle.AspNetCore.Extensions.Extensions;

namespace Exceptionless.Web;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCors(b => b.AddPolicy("AllowAny", p => p
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(isOriginAllowed: _ => true)
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(5))
            .WithExposedHeaders("ETag", Headers.LegacyConfigurationVersion, Headers.ConfigurationVersion, HeaderNames.Link, Headers.RateLimit, Headers.RateLimitRemaining, Headers.ResultCount)));

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.RequireHeaderSymmetry = false;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddControllers(o =>
        {
            o.Filters.Add<ApiExceptionFilter>();
            o.ModelBinderProviders.Insert(0, new CustomAttributesModelBinderProvider());
            o.ModelMetadataDetailsProviders.Add(new NewtonsoftJsonValidationMetadataProvider(new ExceptionlessNamingStrategy()));
            o.InputFormatters.Insert(0, new RawRequestBodyFormatter());
        }).AddNewtonsoftJson(o =>
        {
            o.SerializerSettings.DefaultValueHandling = DefaultValueHandling.Include;
            o.SerializerSettings.NullValueHandling = NullValueHandling.Include;
            o.SerializerSettings.Formatting = Formatting.Indented;
            o.SerializerSettings.ContractResolver = Core.Bootstrapper.GetJsonContractResolver(); // TODO: See if we can resolve this from the di.
        });

        services.AddAutoValidation();
        services.AddProblemDetails(ConfigureProblemDetails);

        services.AddAuthentication(ApiKeyAuthenticationOptions.ApiKeySchema).AddApiKeyAuthentication();
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy(AuthorizationRoles.ClientPolicy, policy => policy.RequireClaim(ClaimTypes.Role, AuthorizationRoles.Client));
            options.AddPolicy(AuthorizationRoles.UserPolicy, policy => policy.RequireClaim(ClaimTypes.Role, AuthorizationRoles.User));
            options.AddPolicy(AuthorizationRoles.GlobalAdminPolicy, policy => policy.RequireClaim(ClaimTypes.Role, AuthorizationRoles.GlobalAdmin));
        });

        services.AddRouting(r =>
        {
            r.LowercaseUrls = true;
            r.ConstraintMap.Add("identifier", typeof(IdentifierRouteConstraint));
            r.ConstraintMap.Add("identifiers", typeof(IdentifiersRouteConstraint));
            r.ConstraintMap.Add("objectid", typeof(ObjectIdRouteConstraint));
            r.ConstraintMap.Add("objectids", typeof(ObjectIdsRouteConstraint));
            r.ConstraintMap.Add("token", typeof(TokenRouteConstraint));
            r.ConstraintMap.Add("tokens", typeof(TokensRouteConstraint));
        });

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v2", new OpenApiInfo
            {
                Title = "Exceptionless API",
                Version = "v2",
                TermsOfService = new Uri("https://exceptionless.com/terms/"),
                Contact = new OpenApiContact
                {
                    Name = "Exceptionless",
                    Email = String.Empty,
                    Url = new Uri("https://github.com/exceptionless/Exceptionless")
                },
                License = new OpenApiLicense
                {
                    Name = "Apache License 2.0",
                    Url = new Uri("https://github.com/exceptionless/Exceptionless/blob/main/LICENSE.txt")
                }
            });

            c.AddSecurityDefinition("Basic", new OpenApiSecurityScheme
            {
                Description = "Basic HTTP Authentication",
                Scheme = "basic",
                Type = SecuritySchemeType.Http
            });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Authorization token. Example: \"Bearer {apikey}\"",
                Scheme = "bearer",
                Type = SecuritySchemeType.Http
            });
            c.AddSecurityDefinition("Token", new OpenApiSecurityScheme
            {
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

            c.AddEnumsWithValuesFixFilters();
            c.SupportNonNullableReferenceTypes();
        });
        services.AddSwaggerGenNewtonsoftSupport();

        var appOptions = AppOptions.ReadFromConfiguration(Configuration);
        Bootstrapper.RegisterServices(services, appOptions, Log.Logger.ToLoggerFactory());
        services.AddSingleton(s =>
        {
            return new ThrottlingOptions
            {
                MaxRequestsForUserIdentifierFunc = userIdentifier => appOptions.ApiThrottleLimit,
                Period = TimeSpan.FromMinutes(15)
            };
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<AppOptions>();
        Core.Bootstrapper.LogConfiguration(app.ApplicationServices, options, Log.Logger.ToLoggerFactory().CreateLogger<Startup>());

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseMiddleware<AllowSynchronousIOMiddleware>();

        var apmConfig = app.ApplicationServices.GetRequiredService<ApmConfig>();
        if (apmConfig.EnableMetrics)
            app.UseOpenTelemetryPrometheusScrapingEndpoint();

        app.UseHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = hcr => hcr.Tags.Contains("Critical") || (options.RunJobsInProcess && hcr.Tags.Contains("AllJobs"))
        });

        var readyTags = new List<string> { "Critical" };
        if (!options.EventSubmissionDisabled)
            readyTags.Add("Storage");
        app.UseReadyHealthChecks(readyTags.ToArray());
        app.UseWaitForStartupActionsBeforeServingRequests();

        if (!String.IsNullOrEmpty(options.ExceptionlessApiKey) && !String.IsNullOrEmpty(options.ExceptionlessServerUrl))
            app.UseExceptionless(ExceptionlessClient.Default);

        app.Use(async (context, next) =>
        {
            if (options.AppMode != AppMode.Development && context.Request.IsLocal() == false)
                context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
            context.Response.Headers.Remove("X-Powered-By");

            await next();
        });

        var serverAddressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();
        bool ssl = options.AppMode != AppMode.Development && serverAddressesFeature is not null && serverAddressesFeature.Addresses.Any(a => a.StartsWith("https://"));

        if (ssl)
            app.UseHttpsRedirection();

        app.UseCsp(csp =>
        {
            csp.AllowFonts.FromSelf()
                .From("https://fonts.gstatic.com")
                .From("https://www.gravatar.com")
                .From("https://fonts.intercomcdn.com")
                .From("https://cdn.jsdelivr.net");
            csp.AllowImages.FromSelf()
                .From("data:")
                .From("https://q.stripe.com")
                .From("https://js.intercomcdn.com")
                .From("https://downloads.intercomcdn.com")
                .From("https://uploads.intercomcdn.com")
                .From("https://static.intercomassets.com")
                .From("https://user-images.githubusercontent.com")
                .From("https://www.gravatar.com")
                .From("http://www.gravatar.com");
            csp.AllowScripts.FromSelf()
                .AllowUnsafeInline()
                .AllowUnsafeEval()
                .From("https://js.stripe.com")
                .From("https://widget.intercom.io")
                .From("https://js.intercomcdn.com")
                .From("https://cdn.jsdelivr.net");
            csp.AllowStyles.FromSelf()
                .AllowUnsafeInline()
                .From("https://fonts.googleapis.com")
                .From("https://cdn.jsdelivr.net");
            csp.AllowConnections.ToSelf()
                .To("https://collector.exceptionless.io")
                .To("https://config.exceptionless.io")
                .To("https://heartbeat.exceptionless.io")
                .To("https://api-iam.intercom.io/")
                .To("wss://nexus-websocket-a.intercom.io");

            csp.OnSendingHeader = context =>
            {
                context.ShouldNotSend = context.HttpContext.Request.Path.StartsWithSegments("/api");
                return Task.CompletedTask;
            };
        });

        app.UseSerilogRequestLogging(o =>
        {
            o.EnrichDiagnosticContext = (context, httpContext) =>
            {
                context.Set("ActivityId", Activity.Current?.Id);
            };
            o.MessageTemplate = "{ActivityId} HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            o.GetLevel = (context, duration, ex) =>
            {
                if (ex is not null || context.Response.StatusCode > 499)
                    return LogEventLevel.Error;

                if (context.Response.StatusCode > 399)
                    return LogEventLevel.Information;

                if (duration < 1000 || context.Request.Path.StartsWithSegments("/api/v2/push"))
                    return LogEventLevel.Debug;

                return LogEventLevel.Information;
            };
        });

        app.UseStaticFiles();
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

        if (options.ApiThrottleLimit < Int32.MaxValue)
        {
            // Throttle api calls to X every 15 minutes by IP address.
            app.UseMiddleware<ThrottlingMiddleware>();
        }

        // Reject event posts in organizations over their max event limits.
        app.UseMiddleware<OverageMiddleware>();

        app.UseSwagger(c => c.RouteTemplate = "docs/{documentName}/swagger.json");
        app.UseSwaggerUI(s =>
        {
            s.RoutePrefix = "docs";
            s.SwaggerEndpoint("/docs/v2/swagger.json", "Exceptionless API");
            s.InjectStylesheet("/docs.css");
        });

        if (options.EnableWebSockets)
        {
            app.UseWebSockets();
            app.UseMiddleware<MessageBusBrokerMiddleware>();
        }

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapFallback("{**slug:nonfile}", CreateRequestDelegate(endpoints, "/index.html"));
        });
    }

    private static void ConfigureProblemDetails(ProblemDetailsOptions options)
    {
        //void SetDetails(ProblemDetails details, int statusCode)
        //{
        //    details.Status = statusCode;
        //    details.Type = GetDefaultType(statusCode);
        //    details.Title = ReasonPhrases.GetReasonPhrase(statusCode);
        //}

        //string GetDefaultType(int statusCode)
        //{
        //    return $"https://httpstatuses.io/{statusCode}";
        //}

        //options.CustomizeProblemDetails = context =>
        //{
        //    var details = context.ProblemDetails;
        //    if (details is ValidationProblemDetails { Status: not 422 })
        //    {
        //        // TODO: Serialization work around until .NET 8 https://github.com/dotnet/aspnetcore/issues/44132
        //        SetDetails(details, StatusCodes.Status422UnprocessableEntity);
        //    }
        //};
    }

    private static RequestDelegate CreateRequestDelegate(IEndpointRouteBuilder endpoints, string filePath)
    {
        var app = endpoints.CreateApplicationBuilder();
        var apiPathSegment = new PathString("/api");
        var docsPathSegment = new PathString("/docs");
        var nextPathSegment = new PathString("/next");
        app.Use(next => context =>
        {
            bool isApiRequest = context.Request.Path.StartsWithSegments(apiPathSegment);
            bool isDocsRequest = context.Request.Path.StartsWithSegments(docsPathSegment);
            bool isNextRequest = context.Request.Path.StartsWithSegments(nextPathSegment);

            if (!isApiRequest && !isDocsRequest && !isNextRequest)
                context.Request.Path = "/" + filePath;
            else if (!isApiRequest && !isDocsRequest)
                context.Request.Path = "/next/" + filePath;

            // Set endpoint to null so the static files middleware will handle the request.
            context.SetEndpoint(null);

            return next(context);
        });

        app.UseStaticFiles();
        return app.Build();
    }
}
