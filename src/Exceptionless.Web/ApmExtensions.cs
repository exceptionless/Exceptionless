using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

public static partial class ApmExtensions
{
    public static IHostBuilder AddApm(this IHostBuilder builder, ApmConfig config)
    {
        var attributes = new Dictionary<string, object>()
        {
            { "service.namespace", config.ServiceNamespace },
            { "deployment.environment", config.DeploymentEnvironment }
        };

        if (!String.IsNullOrEmpty(config.ServiceVersion))
            attributes.Add("service.version", config.ServiceVersion);

        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(config.ServiceName).AddAttributes(attributes);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(config);
            //services.AddHostedService(sp => new SelfDiagnosticsLoggingHostedService(sp.GetRequiredService<ILoggerFactory>(), config.Debug ? EventLevel.Verbose : null));

            services.AddOpenTelemetry().WithTracing(b =>
            {
                b.SetResourceBuilder(resourceBuilder);

                b.AddAspNetCoreInstrumentation(o =>
                {
                    o.Filter = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api/v2/push", StringComparison.OrdinalIgnoreCase))
                            return false;

                        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
                            return false;

                        if (context.Request.Headers.UserAgent.ToString().Contains("HealthChecker"))
                            return false;

                        return true;
                    };
                });

                b.AddElasticsearchClientInstrumentation(c =>
                {
                    c.SuppressDownstreamInstrumentation = true;
                    c.ParseAndFormatRequest = config.FullDetails;
                    c.Enrich = (activity, source, data) =>
                    {
                        // truncate statements
                        if (activity.GetTagItem("db.statement") is string { Length: > 10000 } dbStatement)
                        {
                            dbStatement = _stackIdListShortener.Replace(dbStatement, "$1...]");
                            if (dbStatement.Length > 10000)
                                dbStatement = dbStatement.Substring(0, 10000);

                            activity.SetTag("db.statement", dbStatement);
                        }

                        // 404s should not be error
                        if (activity.GetTagItem("http.status_code") is 404)
                        {
                            activity.SetStatus(ActivityStatusCode.Unset);
                        }
                    };
                });

                b.AddHttpClientInstrumentation();
                b.AddSource("Exceptionless", "Foundatio");

                if (config.EnableRedis)
                    b.AddRedisInstrumentation(c =>
                    {
                        c.EnrichActivityWithTimingEvents = false;
                        c.SetVerboseDatabaseStatements = config.FullDetails;
                    });

                if (config.Console)
                    b.AddConsoleExporter();

                if (config.EnableExporter)
                {
                    b.AddProcessor(new FilteringProcessor(activity =>
                    {
                        // filter out insignificant activities
                        if (config.MinDurationMs > 0 && activity.Duration < TimeSpan.FromMilliseconds(config.MinDurationMs))
                            return false;

                        if (activity.GetTagItem("http.route") is string httpRoute)
                        {
                            // only capture 10% of config requests
                            if (httpRoute == "api/v2/projects/config")
                                return Random.Shared.Next(100) > 90;
                        }

                        if (activity is { DisplayName: "LLEN", Parent: null })
                            return false;

                        if (activity.DisplayName == "Elasticsearch HEAD")
                            return false;

                        if (activity.GetTagItem("db.statement") is not string statement)
                            return true;

                        if (statement.EndsWith("__PING__"))
                            return false;

                        return true;
                    }));

                    b.AddOtlpExporter();
                }
            });

            services.AddOpenTelemetry().WithMetrics(b =>
            {
                b.SetResourceBuilder(resourceBuilder);

                b.AddHttpClientInstrumentation();
                b.AddAspNetCoreInstrumentation();
                b.AddMeter("Exceptionless", "Foundatio");
                b.AddMeter("System.Runtime");
                b.AddRuntimeInstrumentation();
                b.AddProcessInstrumentation();

                if (config.Console)
                    b.AddConsoleExporter((_, metricReaderOptions) =>
                    {
                        // The ConsoleMetricExporter defaults to a manual collect cycle.
                        // This configuration causes metrics to be exported to stdout on a 10s interval.
                        metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 10000;
                    });

                b.AddPrometheusExporter();

                if (config.EnableExporter)
                    b.AddOtlpExporter();
            });
        });

        if (config.EnableLogs)
        {
            builder.ConfigureLogging(l =>
            {
                l.AddOpenTelemetry(o =>
                {
                    o.SetResourceBuilder(resourceBuilder);
                    o.IncludeScopes = true;
                    o.ParseStateValues = true;
                    o.IncludeFormattedMessage = true;

                    if (config.Console)
                        o.AddConsoleExporter();

                    if (config.EnableExporter)
                        o.AddOtlpExporter();
                });
            });
        }

        return builder;
    }

    private static readonly Regex _stackIdListShortener = StackIdListShortenerRegex();

    [GeneratedRegex("(\"stack_id\": \\[)([^\\]]*)\\]", RegexOptions.Compiled)]
    private static partial Regex StackIdListShortenerRegex();
}

public class ApmConfig
{
    private readonly IConfiguration _apmConfig;

    public ApmConfig(IConfigurationRoot config, string processName, string? serviceVersion, bool enableRedis)
    {
        EnableExporter = !string.IsNullOrWhiteSpace(config.GetValue<string>("OTEL_EXPORTER_OTLP_ENDPOINT"));

        _apmConfig = config.GetSection("Apm");
        processName = processName.StartsWith('-') ? processName : "-" + processName;

        ServiceName = _apmConfig.GetValue("ServiceName", "") + processName;
        if (ServiceName.StartsWith('-'))
            ServiceName = ServiceName.Substring(1);

        DeploymentEnvironment = _apmConfig.GetValue("ServiceEnvironment", "dev") ?? throw new InvalidOperationException();
        ServiceNamespace = _apmConfig.GetValue("ServiceNamespace", ServiceName) ?? throw new InvalidOperationException();
        ServiceVersion = serviceVersion;
        EnableRedis = enableRedis;
    }

    public bool EnableExporter { get; }
    public bool EnableLogs => _apmConfig.GetValue("EnableLogs", false);
    public string ServiceName { get; }
    public string DeploymentEnvironment { get; }
    public string ServiceNamespace { get; }
    public string? ServiceVersion { get; }
    public bool FullDetails => _apmConfig.GetValue("FullDetails", false);
    public int MinDurationMs => _apmConfig.GetValue("MinDurationMs", -1);
    public bool EnableRedis { get; }
    public bool Debug => _apmConfig.GetValue("Debug", false);
    public bool Console => _apmConfig.GetValue("Console", false);
}

public class FilteringProcessor : BaseProcessor<Activity>
{
    private readonly Func<Activity, bool> _filter;

    public FilteringProcessor(Func<Activity, bool> filter)
    {
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }

    public override void OnEnd(Activity activity)
    {
        if (!_filter(activity))
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
    }
}
