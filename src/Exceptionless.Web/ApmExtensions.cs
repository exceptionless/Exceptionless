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
            services.AddHostedService(sp => new SelfDiagnosticsLoggingHostedService(sp.GetRequiredService<ILoggerFactory>(), config.Debug ? EventLevel.Verbose : null));

            services.AddOpenTelemetry().WithTracing(b =>
            {
                b.SetResourceBuilder(resourceBuilder);

                b.AddAspNetCoreInstrumentation(o =>
                {
                    o.Filter = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api/v2/push", StringComparison.OrdinalIgnoreCase))
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
                            activity.SetStatus(Status.Unset);
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
                    b.AddFilteredOtlpExporter(c =>
                    {
                        c.Filter = a => a.Duration > TimeSpan.FromMilliseconds(config.MinDurationMs) || a.GetTagItem("db.system") is not null;
                    });
            });

            services.AddOpenTelemetry().WithMetrics(b =>
            {
                b.SetResourceBuilder(resourceBuilder);

                b.AddHttpClientInstrumentation();
                b.AddAspNetCoreInstrumentation();
                b.AddMeter("Exceptionless", "Foundatio");
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

public sealed class CustomFilterProcessor : CompositeProcessor<Activity>
{
    private readonly Func<Activity, bool>? _filter;

    public CustomFilterProcessor(BaseProcessor<Activity> processor, Func<Activity, bool>? filter) : base(new[] { processor })
    {
        _filter = filter;
    }

    public override void OnEnd(Activity activity)
    {
        if (_filter is null || _filter(activity))
            base.OnEnd(activity);
    }
}

public static class CustomFilterProcessorExtensions
{
    public static TracerProviderBuilder AddFilteredOtlpExporter(this TracerProviderBuilder builder, Action<FilteredOtlpExporterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
        {
            return deferredTracerProviderBuilder.Configure((sp, providerBuilder) =>
            {
                var oltpOptions = sp.GetService<IOptions<FilteredOtlpExporterOptions>>()?.Value ?? new FilteredOtlpExporterOptions();
                AddFilteredOtlpExporter(providerBuilder, oltpOptions, configure, sp);
            });
        }

        return AddFilteredOtlpExporter(builder, new FilteredOtlpExporterOptions(), configure, serviceProvider: null);
    }

    internal static TracerProviderBuilder AddFilteredOtlpExporter(
        TracerProviderBuilder builder,
        FilteredOtlpExporterOptions exporterOptions,
        Action<FilteredOtlpExporterOptions>? configure,
        IServiceProvider? serviceProvider,
        Func<BaseExporter<Activity>, BaseExporter<Activity>>? configureExporterInstance = null)
    {

        configure?.Invoke(exporterOptions);

        exporterOptions.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpTraceExporter");

        BaseExporter<Activity> otlpExporter = new OtlpTraceExporter(exporterOptions);

        if (configureExporterInstance is not null)
            otlpExporter = configureExporterInstance(otlpExporter);

        if (exporterOptions.ExportProcessorType == ExportProcessorType.Simple)
        {
            return builder.AddProcessor(new CustomFilterProcessor(new SimpleActivityExportProcessor(otlpExporter), exporterOptions.Filter));
        }

        var batchOptions = exporterOptions.BatchExportProcessorOptions ?? new();
        return builder.AddProcessor(new CustomFilterProcessor(new BatchActivityExportProcessor(
            otlpExporter,
            batchOptions.MaxQueueSize,
            batchOptions.ScheduledDelayMilliseconds,
            batchOptions.ExporterTimeoutMilliseconds,
            batchOptions.MaxExportBatchSize), exporterOptions.Filter));
    }

    public static void TryEnableIHttpClientFactoryIntegration(this OtlpExporterOptions options, IServiceProvider? serviceProvider, string httpClientName)
    {
        // use reflection to call the method
        var exporterExtensionsType = typeof(OtlpExporterOptions).Assembly.GetType("OpenTelemetry.Exporter.OtlpExporterOptionsExtensions");
        exporterExtensionsType?.GetMethod("TryEnableIHttpClientFactoryIntegration")?.Invoke(null, [options,
            serviceProvider!,
            httpClientName
        ]);
    }
}

public class FilteredOtlpExporterOptions : OtlpExporterOptions
{
    public Func<Activity, bool>? Filter { get; set; }
}
