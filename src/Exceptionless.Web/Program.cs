using System.Diagnostics;
using System.Security.Claims;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Serialization;
using Exceptionless.Core.Validation;
using Exceptionless.Insulation.Configuration;
using Exceptionless.Web.Api;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Hubs;
using Exceptionless.Web.Mcp;
using Exceptionless.Web.Security;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.Handlers;
using Exceptionless.Web.Utility.OpenApi;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Mediator;
using Foundatio.Repositories.Exceptions;
using HttpIResult = Microsoft.AspNetCore.Http.IResult;
using Joonasw.AspNetCore.SecurityHeaders;
using Joonasw.AspNetCore.SecurityHeaders.Csp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using ModelContextProtocol.AspNetCore;
using OpenTelemetry;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;

namespace Exceptionless.Web;

public partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            Console.Title = "Exceptionless Web";

            string? environment = Environment.GetEnvironmentVariable("EX_AppMode");
            if (String.IsNullOrWhiteSpace(environment))
                environment = Environments.Production;

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                EnvironmentName = environment
            });
            builder.Configuration.Sources.Clear();
            builder.Configuration
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .AddYamlFile($"appsettings.{environment}.yml", optional: true, reloadOnChange: true)
                .AddYamlFile("appsettings.Local.yml", optional: true, reloadOnChange: true);

            // When running inside WebApplicationFactory, AppContext.BaseDirectory differs from
            // the content root and may contain test-specific configuration overrides.
            string appBaseDir = Path.GetFullPath(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            string contentRoot = Path.GetFullPath(builder.Environment.ContentRootPath.TrimEnd(Path.DirectorySeparatorChar));
            if (!appBaseDir.Equals(contentRoot, StringComparison.OrdinalIgnoreCase))
            {
                builder.Configuration.AddYamlFile(
                    new Microsoft.Extensions.FileProviders.PhysicalFileProvider(appBaseDir),
                    "appsettings.yml", optional: true, reloadOnChange: false);
            }

            builder.Configuration
                .AddCustomEnvironmentVariables()
                .AddCommandLine(args);

            var configuration = (IConfigurationRoot)builder.Configuration;
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateBootstrapLogger()
                .ForContext<Program>();

            var options = AppOptions.ReadFromConfiguration(configuration);
            options.QueueOptions.MetricsPollingEnabled = options.RunJobsInProcess;

            var apmConfig = new ApmConfig(configuration, "web", options.InformationalVersion, options.CacheOptions.Provider == "redis");

            Log.Information("Bootstrapping Exceptionless Web in {AppMode} mode ({InformationalVersion}) on {MachineName} with scope {AppScope}", environment, options.InformationalVersion, Environment.MachineName, options.AppScope);

            SetClientEnvironmentVariablesInDevelopmentMode(options);

            builder.Logging.ClearProviders();

            builder.Host
                .UseSerilog((ctx, sp, c) =>
                {
                    c.ReadFrom.Configuration(ctx.Configuration);
                    c.ReadFrom.Services(sp);
                    c.Enrich.WithMachineName();

                    if (!String.IsNullOrEmpty(options.ExceptionlessApiKey))
                        c.WriteTo.Sink(new ExceptionlessSink(), LogEventLevel.Information);
                }, writeToProviders: true)
                .AddApm(apmConfig);

            builder.WebHost.ConfigureKestrel(c =>
            {
                c.AddServerHeader = false;

                if (options.MaximumEventPostSize > 0)
                    c.Limits.MaxRequestBodySize = options.MaximumEventPostSize + EventPostRequestBodyStream.KestrelBodyLimitSlopBytes;
            });

            builder.Services.AddSingleton(configuration);
            builder.Services.AddSingleton(apmConfig);
            builder.Services.AddAppOptions(options);
            builder.Services.AddHttpContextAccessor();

            builder.Services.AddCors(b => b.AddPolicy("AllowAny", p => p
                .AllowAnyHeader()
                .AllowAnyMethod()
                .SetIsOriginAllowed(isOriginAllowed: _ => true)
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(5))
                .WithExposedHeaders("ETag", Headers.LegacyConfigurationVersion, Headers.ConfigurationVersion, HeaderNames.Link, Headers.RateLimit, Headers.RateLimitRemaining, Headers.ResultCount)));

            builder.Services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.RequireHeaderSymmetry = false;
                o.KnownIPNetworks.Clear();
                o.KnownProxies.Clear();
            });

            builder.Services.ConfigureHttpJsonOptions(o =>
            {
                o.SerializerOptions.ConfigureExceptionlessApiDefaults();
                o.SerializerOptions.Converters.Add(new DeltaJsonConverterFactory());
            });

            builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = CustomizeProblemDetails);
            builder.Services.AddExceptionHandler<ExceptionToProblemDetailsHandler>();

            builder.Services.AddAuthentication(ApiKeyAuthenticationOptions.ApiKeySchema).AddApiKeyAuthentication();
            builder.Services.AddAuthorization(o =>
            {
                o.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                o.AddPolicy(AuthorizationRoles.ClientPolicy, policy => policy.RequireClaim(ClaimTypes.Role, AuthorizationRoles.Client));
                o.AddPolicy(AuthorizationRoles.UserPolicy, policy => policy.RequireClaim(ClaimTypes.Role, AuthorizationRoles.User));
                o.AddPolicy(AuthorizationRoles.GlobalAdminPolicy, policy => policy.RequireClaim(ClaimTypes.Role, AuthorizationRoles.GlobalAdmin));
                o.AddPolicy(AuthorizationRoles.McpPolicy, policy => policy.RequireClaim(ClaimTypes.Role, AuthorizationRoles.McpRead));
                o.AddPolicy(AuthorizationRoles.ProjectsReadPolicy, policy => policy.RequireAssertion(context => context.User.IsInRole(AuthorizationRoles.User) || context.User.IsInRole(AuthorizationRoles.ProjectsRead)));
                o.AddPolicy(AuthorizationRoles.StacksReadPolicy, policy => policy.RequireAssertion(context => context.User.IsInRole(AuthorizationRoles.User) || context.User.IsInRole(AuthorizationRoles.StacksRead)));
                o.AddPolicy(AuthorizationRoles.StacksWritePolicy, policy => policy.RequireAssertion(context => context.User.IsInRole(AuthorizationRoles.User) || context.User.IsInRole(AuthorizationRoles.StacksWrite)));
                o.AddPolicy(AuthorizationRoles.EventsReadPolicy, policy => policy.RequireAssertion(context => context.User.IsInRole(AuthorizationRoles.User) || context.User.IsInRole(AuthorizationRoles.EventsRead)));
                o.AddPolicy(AuthorizationRoles.SourceMapsWritePolicy, policy => policy.RequireAssertion(context => context.User.IsInRole(AuthorizationRoles.User) || context.User.IsInRole(AuthorizationRoles.SourceMapsWrite)));
            });

            builder.Services.AddRouting(r =>
            {
                r.LowercaseUrls = true;
                r.ConstraintMap.Add("identifier", typeof(IdentifierRouteConstraint));
                r.ConstraintMap.Add("identifiers", typeof(IdentifiersRouteConstraint));
                r.ConstraintMap.Add("objectid", typeof(ObjectIdRouteConstraint));
                r.ConstraintMap.Add("objectids", typeof(ObjectIdsRouteConstraint));
                r.ConstraintMap.Add("token", typeof(TokenRouteConstraint));
                r.ConstraintMap.Add("tokens", typeof(TokensRouteConstraint));
            });

            builder.Services.AddExceptionlessOpenApi();

            builder.Services.AddSingleton<IMediatorResultMapper<HttpIResult>, ApiResultMapper>();
            builder.Services.AddMediator()
                .ConfigureResultMapping<HttpIResult>(resultMapping => resultMapping
                    .MapStatus(ResultStatus.BadRequest, ApiResultMapper.MapBadRequest)
                    .MapStatus(ResultStatus.Invalid, ApiResultMapper.MapValidation)
                    .MapStatus(ResultStatus.NotFound, ApiResultMapper.MapNotFound)
                    .MapStatus(ResultStatus.Unauthorized, ApiResultMapper.MapUnauthorized)
                    .MapStatus(ResultStatus.Forbidden, ApiResultMapper.MapForbidden)
                    .MapStatus(ResultStatus.Conflict, ApiResultMapper.MapConflict)
                    .MapStatus(ResultStatus.Error, ApiResultMapper.MapError)
                    .MapStatus(ResultStatus.CriticalError, ApiResultMapper.MapCriticalError)
                    .MapStatus(ResultStatus.Unavailable, ApiResultMapper.MapUnavailable));
            Bootstrapper.RegisterServices(builder.Services, options, Log.Logger.ToLoggerFactory());
            builder.Services.AddScoped<McpContextService>();
            builder.Services.AddSingleton<ISessionMigrationHandler, McpSessionMigrationHandler>();
            builder.Services.AddMcpServer()
                .WithHttpTransport(o => o.Stateless = false)
                .WithTools<ExceptionlessMcpTools>();
            builder.Services.AddSingleton(_ => new ThrottlingOptions
            {
                MaxRequestsForUserIdentifierFunc = _ => options.ApiThrottleLimit,
                Period = TimeSpan.FromMinutes(15)
            });

            var app = builder.Build();

            Core.Bootstrapper.LogConfiguration(app.Services, options, app.Services.GetRequiredService<ILogger<Program>>());

            app.UseExceptionHandler(new ExceptionHandlerOptions
            {
                StatusCodeSelector = ex => ex switch
                {
                    UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                    MiniValidatorException => StatusCodes.Status422UnprocessableEntity,
                    BadHttpRequestException badRequest => badRequest.StatusCode,
                    ApplicationException applicationException when applicationException.Message.Contains("version_conflict") => StatusCodes.Status409Conflict,
                    VersionConflictDocumentException => StatusCodes.Status409Conflict,
                    NotImplementedException => StatusCodes.Status501NotImplemented,
                    _ => StatusCodes.Status500InternalServerError
                }
            });
            app.UseStatusCodePages(WriteProblemDetailsStatusCodeResponseAsync);

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
                if (options.AppMode != AppMode.Development && !context.Request.IsLocal())
                    context.Response.Headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";

                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                context.Response.Headers.XContentTypeOptions = "nosniff";
                context.Response.Headers.XFrameOptions = "DENY";
                context.Response.Headers.XXSSProtection = "1; mode=block";
                context.Response.Headers.Remove("X-Powered-By");

                await next();
            });

            var serverAddressesFeature = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
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
                app.UseMiddleware<ThrottlingMiddleware>();

            app.UseMiddleware<OverageMiddleware>();

            if (options.EnableWebSockets)
            {
                app.UseWebSockets();
                app.UseMiddleware<MessageBusBrokerMiddleware>();
            }

            app.MapOpenApi("/docs/v2/openapi.json");
            app.MapScalarApiReference("/docs", o =>
            {
                o.WithOpenApiRoutePattern("/docs/{documentName}/openapi.json")
                    .AddDocument("v2", "Exceptionless API", "/docs/{documentName}/openapi.json", true)
                    .AddPreferredSecuritySchemes("Bearer");
            });
            app.MapApiEndpoints();
            app.MapMcp("/mcp").RequireAuthorization(AuthorizationRoles.McpPolicy);
            app.MapFallback("{**slug:nonfile}", CreateRequestDelegate(app, "/index.html"));

            await app.RunAsync();
            return 0;
        }
        catch (Exception ex) when (ex is not HostAbortedException)
        {
            Log.Fatal(ex, "Job host terminated unexpectedly");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
            await ExceptionlessClient.Default.ProcessQueueAsync();

            if (Debugger.IsAttached)
                Console.ReadKey();
        }
    }

    private static void CustomizeProblemDetails(ProblemDetailsContext ctx)
    {
        ctx.ProblemDetails.Instance = $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
        if (ctx.HttpContext.Items.TryGetValue("reference-id", out object? refId) && refId is string referenceId)
            ctx.ProblemDetails.Extensions.Add("reference-id", referenceId);

        if (ctx.HttpContext.Items.TryGetValue("errors", out object? value) && value is Dictionary<string, string[]> errors)
            ctx.ProblemDetails.Extensions.Add("errors", errors);

        if (ctx.ProblemDetails is ValidationProblemDetails validationProblem)
        {
            validationProblem.Errors = validationProblem.Errors
                .ToDictionary(
                    error => error.Key.ToLowerUnderscoredWords(),
                    error => error.Value
                );
        }
    }

    internal static Task WriteProblemDetailsStatusCodeResponseAsync(StatusCodeContext statusCodeContext)
    {
        return TypedResults
            .Problem(statusCode: statusCodeContext.HttpContext.Response.StatusCode)
            .ExecuteAsync(statusCodeContext.HttpContext);
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

            context.SetEndpoint(null);
            return next(context);
        });

        app.UseStaticFiles();
        return app.Build();
    }

    private static void SetClientEnvironmentVariablesInDevelopmentMode(AppOptions options)
    {
        if (options.AppMode is not AppMode.Development)
            return;

        Log.Debug("Updating client environment variables");
        try
        {
            Environment.SetEnvironmentVariable("PUBLIC_BASE_URL", options.BaseURL);
            Environment.SetEnvironmentVariable("PUBLIC_ENABLE_ACCOUNT_CREATION",
                options.AuthOptions.EnableAccountCreation.ToString().ToLower());
            Environment.SetEnvironmentVariable("PUBLIC_SYSTEM_NOTIFICATION_MESSAGE", options.NotificationMessage);
            Environment.SetEnvironmentVariable("PUBLIC_EXCEPTIONLESS_API_KEY", options.ExceptionlessApiKey);
            Environment.SetEnvironmentVariable("PUBLIC_EXCEPTIONLESS_SERVER_URL", options.ExceptionlessServerUrl);
            Environment.SetEnvironmentVariable("PUBLIC_STRIPE_PUBLISHABLE_KEY",
                options.StripeOptions.StripePublishableApiKey);
            Environment.SetEnvironmentVariable("PUBLIC_FACEBOOK_APPID", options.AuthOptions.FacebookId);
            Environment.SetEnvironmentVariable("PUBLIC_GITHUB_APPID", options.AuthOptions.GitHubId);
            Environment.SetEnvironmentVariable("PUBLIC_GOOGLE_APPID", options.AuthOptions.GoogleId);
            Environment.SetEnvironmentVariable("PUBLIC_MICROSOFT_APPID", options.AuthOptions.MicrosoftId);
            Environment.SetEnvironmentVariable("PUBLIC_INTERCOM_APPID", options.IntercomOptions.IntercomId);
            Environment.SetEnvironmentVariable("PUBLIC_SLACK_APPID", options.SlackOptions.SlackId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating client environment variables");
        }
    }
}
