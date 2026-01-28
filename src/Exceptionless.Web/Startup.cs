using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Serialization;
using Exceptionless.Core.Validation;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Hubs;
using Exceptionless.Web.Models;
using Exceptionless.Web.Security;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.Handlers;
using Exceptionless.Web.Utility.OpenApi;
using FluentValidation;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Repositories.Exceptions;
using Joonasw.AspNetCore.SecurityHeaders;
using Joonasw.AspNetCore.SecurityHeaders.Csp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

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

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.RequireHeaderSymmetry = false;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

        services.AddControllers(o =>
        {
            o.ModelBinderProviders.Insert(0, new CustomAttributesModelBinderProvider());
            o.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider(LowerCaseUnderscoreNamingPolicy.Instance));
            o.InputFormatters.Insert(0, new RawRequestBodyFormatter());
        })
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.ConfigureExceptionlessDefaults();
            o.JsonSerializerOptions.Converters.Add(new DeltaJsonConverterFactory());
        });

        // Have to add this to get the open api json file to be snake case.
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.ConfigureExceptionlessDefaults();
            o.SerializerOptions.Converters.Add(new DeltaJsonConverterFactory());
        });

        services.AddProblemDetails(o => o.CustomizeProblemDetails = CustomizeProblemDetails);
        services.AddExceptionHandler<ExceptionToProblemDetailsHandler>();
        services.AddAutoValidation();

        services.AddAuthentication(ApiKeyAuthenticationOptions.ApiKeySchema).AddApiKeyAuthentication();
        services.AddAuthorization(o =>
        {
            o.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            o.AddPolicy(AuthorizationRoles.ClientPolicy, policy => policy.RequireClaim(ClaimTypes.Role, AuthorizationRoles.Client));
            o.AddPolicy(AuthorizationRoles.UserPolicy, policy => policy.RequireClaim(ClaimTypes.Role, AuthorizationRoles.User));
            o.AddPolicy(AuthorizationRoles.GlobalAdminPolicy, policy => policy.RequireClaim(ClaimTypes.Role, AuthorizationRoles.GlobalAdmin));
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

        services.AddOpenApi(o =>
        {
            // Customize schema names to match legacy SwashBuckle naming for backwards compatibility
            o.CreateSchemaReferenceId = SchemaReferenceIdHelper.CreateSchemaReferenceId;

            // Document transformers (run on entire document)
            o.AddDocumentTransformer<AggregateDocumentTransformer>();
            o.AddDocumentTransformer<DocumentInfoTransformer>();
            o.AddDocumentTransformer<RemoveProblemJsonFromSuccessResponsesTransformer>();

            // Operation transformers (run on each operation)
            o.AddOperationTransformer<RequestBodyContentOperationTransformer>();
            o.AddOperationTransformer<XmlDocumentationOperationTransformer>();

            // Schema transformers (run on each schema) - alphabetical order
            o.AddSchemaTransformer<DataAnnotationsSchemaTransformer>();
            o.AddSchemaTransformer<DeltaSchemaTransformer>();
            o.AddSchemaTransformer<DictionarySubclassSchemaTransformer>();
            o.AddSchemaTransformer<NumericTypeSchemaTransformer>();
            o.AddSchemaTransformer<ReadOnlyPropertySchemaTransformer>();
            o.AddSchemaTransformer<RequiredPropertySchemaTransformer>();
            o.AddSchemaTransformer<UniqueItemsSchemaTransformer>();
            o.AddSchemaTransformer<XEnumNamesSchemaTransformer>();
        });

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

    private void CustomizeProblemDetails(ProblemDetailsContext ctx)
    {
        ctx.ProblemDetails.Extensions.Add("instance", $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}");
        if (ctx.HttpContext.Items.TryGetValue("reference-id", out object? refId) && refId is string referenceId)
        {
            ctx.ProblemDetails.Extensions.Add("reference-id", referenceId);
        }

        if (ctx.HttpContext.Items.TryGetValue("errors", out object? value) && value is Dictionary<string, string[]> errors)
        {
            ctx.ProblemDetails.Extensions.Add("errors", errors);
        }

        if (ctx.ProblemDetails is ValidationProblemDetails validationProblem)
        {
            // This might be possible to accomplish via serializer.
            // NOTE: the key could be wrong for things like ExternalAuthInfo where the keys are camel case.
            validationProblem.Errors = validationProblem.Errors
                .ToDictionary(
                    error => error.Key.ToLowerUnderscoredWords(),
                    error => error.Value
                );
        }

        // errors
        // TODO: Check casing of property names of model state validation errors.
    }

    public void Configure(IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<AppOptions>();
        Core.Bootstrapper.LogConfiguration(app.ApplicationServices, options, Log.Logger.ToLoggerFactory().CreateLogger<Startup>());

        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            StatusCodeSelector = ex => ex switch
            {
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                ValidationException => StatusCodes.Status422UnprocessableEntity,
                MiniValidatorException => StatusCodes.Status422UnprocessableEntity,
                ApplicationException applicationException when applicationException.Message.Contains("version_conflict") => StatusCodes.Status409Conflict,
                VersionConflictDocumentException => StatusCodes.Status409Conflict,
                NotImplementedException => StatusCodes.Status501NotImplemented,
                _ => StatusCodes.Status500InternalServerError
            }
        });
        app.UseStatusCodePages();

        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        app.UseHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = hcr => hcr.Tags.Contains("Critical") || (options.RunJobsInProcess && hcr.Tags.Contains("AllJobs"))
        });

        List<string> readyTags = ["Critical"];
        if (!options.EventSubmissionDisabled)
            readyTags.Add("Storage");
        app.UseReadyHealthChecks(readyTags.ToArray());
        app.UseWaitForStartupActionsBeforeServingRequests();

        if (!String.IsNullOrEmpty(options.ExceptionlessApiKey) && !String.IsNullOrEmpty(options.ExceptionlessServerUrl))
            app.UseExceptionless(ExceptionlessClient.Default);

        app.Use(async (context, next) =>
        {
            if (options.AppMode != AppMode.Development && context.Request.IsLocal() == false)
                context.Response.Headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";

            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers.XXSSProtection = "1; mode=block";
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

            csp.OnSendingHeader = new Func<CspSendingHeaderContext, Task>(context =>
            {
                context.ShouldNotSend = context.HttpContext.Request.Path.StartsWithSegments("/api");
                return Task.CompletedTask;
            });
        });

        app.UseSerilogRequestLogging(o =>
        {
            o.EnrichDiagnosticContext = (context, httpContext) =>
            {
                if (Activity.Current?.Id is not null)
                    context.Set("ActivityId", Activity.Current.Id);
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

        if (options.EnableWebSockets)
        {
            app.UseWebSockets();
            app.UseMiddleware<MessageBusBrokerMiddleware>();
        }

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapOpenApi("/docs/v2/openapi.json");
            endpoints.MapScalarApiReference("/docs", o =>
            {
                o.WithOpenApiRoutePattern("/docs/{documentName}/openapi.json")
                    .AddDocument("v2", "Exceptionless API", "/docs/{documentName}/openapi.json", true)
                    .AddPreferredSecuritySchemes("Bearer");
            });

            endpoints.MapControllers();
            endpoints.MapFallback("{**slug:nonfile}", CreateRequestDelegate(endpoints, "/index.html"));
        });
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
