using System.Diagnostics;
using System.Text.RegularExpressions;
using Exceptionless;
using Exceptionless.Core;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace OpenTelemetry;

public static partial class ServiceDefaults
{
    public static IHostBuilder AddServiceDefaults(this IHostBuilder builder, OpenTelemetryConfig config, string? serviceName = null)
    {
        builder.ConfigureOpenTelemetry(config, serviceName);

        return builder;
    }

    public static IHostBuilder ConfigureOpenTelemetry(this IHostBuilder builder, OpenTelemetryConfig config, string? serviceName = null)
    {
        var attributes = new Dictionary<string, object>();

        if (!String.IsNullOrEmpty(config.ServiceNamespace))
            attributes.Add("service.namespace", config.ServiceNamespace);

        if (!String.IsNullOrEmpty(config.ServiceEnvironment))
            attributes.Add("service.environment", config.ServiceEnvironment);

        if (!String.IsNullOrEmpty(config.ServiceVersion))
            attributes.Add("service.version", config.ServiceVersion);

        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(config.ServiceName).AddAttributes(attributes);

        builder.ConfigureServices(s => s.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.SetResourceBuilder(resourceBuilder);
                metrics.AddRuntimeInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddMeter("Foundatio")
                    .AddMeter(AppDiagnostics.Meter.Name)
                    .AddBuiltInMeters();
            })
            .WithTracing(tracing =>
            {
                tracing.SetResourceBuilder(resourceBuilder);

                tracing.AddAspNetCoreInstrumentation(o =>
                {
                    o.Filter = context =>
                    {
                        return !context.Request.Headers.UserAgent.ToString().Contains("HealthChecker");
                    };
                });

                tracing.AddHttpClientInstrumentation()
                       .AddSource("Foundatio")
                       .AddSource(AppDiagnostics.ActivitySource.Name);

                if (config.EnableRedis)
                    tracing.AddRedisInstrumentation(ConnectionMultiplexer.Connect(""), c =>
                    {
                        //c.EnrichActivityWithTimingEvents = false;
                        c.SetVerboseDatabaseStatements = config.FullDetails;
                    });
            }));

        builder.ConfigureLogging(l => l.AddOpenTelemetry(o =>
        {
            o.SetResourceBuilder(resourceBuilder);
            o.IncludeScopes = true;
            o.ParseStateValues = true;
            o.IncludeFormattedMessage = true;
        }));

        builder.AddOpenTelemetryExporters(config);

        return builder;
    }

    private static IHostBuilder AddOpenTelemetryExporters(this IHostBuilder builder, OpenTelemetryConfig config)
    {
        if (config.UseOtlpExporter)
        {
            builder.ConfigureServices(s =>
            {
                s.Configure<OpenTelemetryLoggerOptions>(logging => logging.AddOtlpExporter());
                s.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter());
                s.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddFilteredOtlpExporter(o =>
                {
                    o.Filter = (activity) =>
                    {
                        if (activity.DisplayName.Contains("health") || activity.DisplayName == "master" || activity.DisplayName.Contains("/api/events"))
                            return false;

                        return true;
                    };
                }));
            });
        }

        builder.ConfigureServices(s => s.AddOpenTelemetry()
           .WithMetrics(metrics => metrics.AddPrometheusExporter()));

        return builder;
    }

    private static MeterProviderBuilder AddBuiltInMeters(this MeterProviderBuilder meterProviderBuilder) =>
        meterProviderBuilder.AddMeter(
            "Microsoft.AspNetCore.Hosting",
            "Microsoft.AspNetCore.Server.Kestrel",
            "System.Net.Http");

    private static readonly Regex _stackIdListShortener = StackIdListShortenerRegex();

    [GeneratedRegex("(\"stack_id\": \\[)([^\\]]*)\\]", RegexOptions.Compiled)]
    private static partial Regex StackIdListShortenerRegex();
}

public class OpenTelemetryConfig
{
    private readonly IConfiguration _config;

    public OpenTelemetryConfig(IConfigurationRoot config, string? serviceName, bool enableRedis)
    {
        UseOtlpExporter = !string.IsNullOrWhiteSpace(config["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        _config = config.GetSection("Apm");
        serviceName = serviceName != null ? serviceName.StartsWith('-') ? serviceName : "-" + serviceName : "";
        ServiceName = _config.GetValue("ServiceName", "") + serviceName;
        if (ServiceName.StartsWith('-'))
            ServiceName = ServiceName.Substring(1);
        ServiceEnvironment = _config.GetValue("ServiceEnvironment", "") ?? throw new InvalidOperationException();
        ServiceNamespace = _config.GetValue("ServiceNamespace", ServiceName) ?? throw new InvalidOperationException();
        ServiceVersion = AppOptions.GetInformationalVersion();
        EnableRedis = enableRedis;
    }

    public bool UseOtlpExporter { get; }
    public string ServiceName { get; }
    public string ServiceEnvironment { get; }
    public string ServiceNamespace { get; }
    public string? ServiceVersion { get; }
    public bool FullDetails => _config.GetValue("FullDetails", false);
    public int MinDurationMs => _config.GetValue<int>("MinDurationMs", -1);
    public bool EnableRedis { get; }
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
            return deferredTracerProviderBuilder.Configure((sp, builder) =>
            {
                var oltpOptions = sp.GetService<IOptions<FilteredOtlpExporterOptions>>()?.Value ?? new FilteredOtlpExporterOptions();
                AddFilteredOtlpExporter(builder, oltpOptions, configure, sp);
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
        else
        {
            var batchOptions = exporterOptions.BatchExportProcessorOptions ?? new();

            return builder.AddProcessor(new CustomFilterProcessor(new BatchActivityExportProcessor(
                otlpExporter,
                batchOptions.MaxQueueSize,
                batchOptions.ScheduledDelayMilliseconds,
                batchOptions.ExporterTimeoutMilliseconds,
                batchOptions.MaxExportBatchSize), exporterOptions.Filter));
        }
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
