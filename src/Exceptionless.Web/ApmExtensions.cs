using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace OpenTelemetry
{
    public static partial class ApmExtensions
    {
        public static IHostBuilder AddApm(this IHostBuilder builder, ApmConfig config)
        {
            // check if everything is disabled
            if (!config.IsEnabled)
            {
                Log.Information("APM is disabled");
                return builder;
            }

            string apiKey = config.ApiKey;
            if (!String.IsNullOrEmpty(apiKey) && apiKey.Length > 6)
                apiKey = String.Concat(apiKey.AsSpan(0, 6), "***");

            Log.Information("Configuring APM: Endpoint={Endpoint} ApiKey={ApiKey} EnableTracing={EnableTracing} EnableLogs={EnableLogs} FullDetails={FullDetails} EnableRedis={EnableRedis} SampleRate={SampleRate}",
                config.Endpoint, apiKey, config.EnableTracing, config.EnableLogs, config.FullDetails, config.EnableRedis, config.SampleRate);

            var resourceBuilder = ResourceBuilder.CreateDefault().AddService(config.ServiceName).AddAttributes(new[] {
                new KeyValuePair<string, object>("service.namespace", config.ServiceNamespace),
                new KeyValuePair<string, object>("service.environment", config.ServiceEnvironment),
                new KeyValuePair<string, object>("service.version", config.ServiceVersion)
            });

            builder.ConfigureServices(services =>
            {
                services.AddSingleton(config);
                services.AddHostedService(sp => new SelfDiagnosticsLoggingHostedService(sp.GetRequiredService<ILoggerFactory>(), config.Debug ? EventLevel.Verbose : null));

                if (config.EnableTracing)
                    services.AddOpenTelemetry().WithTracing(b =>
                    {
                        b.SetResourceBuilder(resourceBuilder);

                        b.AddAspNetCoreInstrumentation(o =>
                        {
                            o.Filter = context =>
                            {
                                return !context.Request.Headers.UserAgent.ToString().Contains("HealthChecker");
                            };
                        });

                        b.AddElasticsearchClientInstrumentation(c =>
                        {
                            c.SuppressDownstreamInstrumentation = true;
                            c.ParseAndFormatRequest = config.FullDetails;
                            c.Enrich = (activity, source, data) =>
                            {
                                // truncate statements
                                if (activity.GetTagItem("db.statement") is string dbStatement && dbStatement.Length > 10000)
                                {
                                    dbStatement = _stackIdListShortener.Replace(dbStatement, "$1...]");
                                    if (dbStatement.Length > 10000)
                                        dbStatement = dbStatement.Substring(0, 10000);

                                    activity.SetTag("db.statement", dbStatement);
                                }

                                // 404s should not be error
                                var httpStatus = activity.GetTagItem("http.status_code") as int?;
                                if (httpStatus.HasValue && httpStatus.Value == 404)
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

                        b.SetSampler(new TraceIdRatioBasedSampler(config.SampleRate));

                        if (config.Console)
                            b.AddConsoleExporter();

                        if (!String.IsNullOrEmpty(config.Endpoint))
                        {
                            if (config.MinDurationMs > 0)
                            {
                                // filter out insignificant activities
                                b.AddFilteredOtlpExporter(c =>
                                {
                                    if (config.Insecure || !String.IsNullOrEmpty(config.SslThumbprint))
                                        c.Protocol = OtlpExportProtocol.HttpProtobuf;

                                    if (!String.IsNullOrEmpty(config.Endpoint))
                                        c.Endpoint = new Uri(config.Endpoint);
                                    if (!String.IsNullOrEmpty(config.ApiKey))
                                        c.Headers = $"api-key={config.ApiKey}";

                                    c.Filter = a => a.Duration > TimeSpan.FromMilliseconds(config.MinDurationMs) || a.GetTagItem("db.system") is not null;
                                });
                            }
                            else
                            {
                                b.AddOtlpExporter(c =>
                                {
                                    if (config.Insecure || !String.IsNullOrEmpty(config.SslThumbprint))
                                        c.Protocol = OtlpExportProtocol.HttpProtobuf;

                                    if (!String.IsNullOrEmpty(config.Endpoint))
                                        c.Endpoint = new Uri(config.Endpoint);
                                    if (!String.IsNullOrEmpty(config.ApiKey))
                                        c.Headers = $"api-key={config.ApiKey}";
                                });
                            }
                        }
                    });

                services.AddOpenTelemetry().WithMetrics(b =>
                {
                    b.SetResourceBuilder(resourceBuilder);

                    b.AddHttpClientInstrumentation();
                    b.AddAspNetCoreInstrumentation();
                    b.AddMeter("Exceptionless", "Foundatio");
                    b.AddRuntimeInstrumentation();

                    if (config.Console)
                        b.AddConsoleExporter((exporterOptions, metricReaderOptions) =>
                        {
                            // The ConsoleMetricExporter defaults to a manual collect cycle.
                            // This configuration causes metrics to be exported to stdout on a 10s interval.
                            metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 10000;
                        });

                    b.AddPrometheusExporter();

                    if (!String.IsNullOrEmpty(config.Endpoint))
                        b.AddOtlpExporter((c, o) =>
                        {
                            if (config.Insecure || !String.IsNullOrEmpty(config.SslThumbprint))
                                c.Protocol = OtlpExportProtocol.HttpProtobuf;

                            // needed for newrelic compatibility until they support cumulative
                            o.TemporalityPreference = MetricReaderTemporalityPreference.Delta;

                            if (!String.IsNullOrEmpty(config.Endpoint))
                                c.Endpoint = new Uri(config.Endpoint);
                            if (!String.IsNullOrEmpty(config.ApiKey))
                                c.Headers = $"api-key={config.ApiKey}";
                        });
                });

                if (config.EnableLogs)
                {
                    services.AddSingleton<ILoggerProvider, OpenTelemetryLoggerProvider>();
                    services.Configure<OpenTelemetryLoggerOptions>(o =>
                    {
                        o.SetResourceBuilder(resourceBuilder);
                        o.IncludeScopes = true;
                        o.ParseStateValues = true;
                        o.IncludeFormattedMessage = true;

                        if (config.Console)
                            o.AddConsoleExporter();

                        if (!String.IsNullOrEmpty(config.Endpoint))
                        {
                            o.AddOtlpExporter(c =>
                            {
                                if (config.Insecure || !String.IsNullOrEmpty(config.SslThumbprint))
                                    c.Protocol = OtlpExportProtocol.HttpProtobuf;

                                if (!String.IsNullOrEmpty(config.Endpoint))
                                    c.Endpoint = new Uri(config.Endpoint);
                                if (!String.IsNullOrEmpty(config.ApiKey))
                                    c.Headers = $"api-key={config.ApiKey}";
                            });
                        }
                    });
                }
            });

            return builder;
        }

        private static readonly Regex _stackIdListShortener = StackIdListShortenerRegex();

        [GeneratedRegex("(\"stack_id\": \\[)([^\\]]*)\\]", RegexOptions.Compiled)]
        private static partial Regex StackIdListShortenerRegex();
    }

    public class ApmConfig
    {
        private readonly IConfiguration _apmConfig;

        public ApmConfig(IConfigurationRoot config, string processName, string serviceVersion, bool enableRedis)
        {
            _apmConfig = config.GetSection("Apm");
            processName = processName.StartsWith("-") ? processName : "-" + processName;

            ServiceName = _apmConfig.GetValue("ServiceName", "") + processName;
            if (ServiceName.StartsWith("-"))
                ServiceName = ServiceName.Substring(1);

            ServiceEnvironment = _apmConfig.GetValue("ServiceEnvironment", "");
            ServiceNamespace = _apmConfig.GetValue("ServiceNamespace", ServiceName);
            ServiceVersion = serviceVersion;
            EnableRedis = enableRedis;
        }

        public bool IsEnabled => EnableLogs || EnableMetrics || EnableTracing;

        public bool EnableLogs => _apmConfig.GetValue("EnableLogs", false);
        public bool EnableMetrics => _apmConfig.GetValue("EnableMetrics", true);
        public bool EnableTracing => _apmConfig.GetValue("EnableTracing", _apmConfig.GetValue("Enabled", false));
        public bool Insecure => _apmConfig.GetValue("Insecure", false);
        public string SslThumbprint => _apmConfig.GetValue("SslThumbprint", String.Empty);
        public string ServiceName { get; }
        public string ServiceEnvironment { get; }
        public string ServiceNamespace { get; }
        public string ServiceVersion { get; }
        public string Endpoint => _apmConfig.GetValue("Endpoint", String.Empty);
        public string ApiKey => _apmConfig.GetValue("ApiKey", String.Empty);
        public bool FullDetails => _apmConfig.GetValue("FullDetails", false);
        public double SampleRate => _apmConfig.GetValue("SampleRate", 1.0);
        public int MinDurationMs => _apmConfig.GetValue<int>("MinDurationMs", -1);
        public bool EnableRedis { get; }
        public bool Debug => _apmConfig.GetValue("Debug", false);
        public bool Console => _apmConfig.GetValue("Console", false);
    }

    public sealed class CustomFilterProcessor : CompositeProcessor<Activity>
    {
        private readonly Func<Activity, bool> _filter;

        public CustomFilterProcessor(BaseProcessor<Activity> processor, Func<Activity, bool> filter) : base(new[] { processor })
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
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

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
            Action<FilteredOtlpExporterOptions> configure,
            IServiceProvider serviceProvider,
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

        public static void TryEnableIHttpClientFactoryIntegration(this OtlpExporterOptions options, IServiceProvider serviceProvider, string httpClientName)
        {
            // use reflection to call the method
            var exporterExtensionsType = typeof(OtlpExporterOptions).Assembly.GetType("OpenTelemetry.Exporter.OtlpExporterOptionsExtensions");
            exporterExtensionsType
                .GetMethod("TryEnableIHttpClientFactoryIntegration")
                .Invoke(null, new object[] { options, serviceProvider, httpClientName });
        }
    }

    public class FilteredOtlpExporterOptions : OtlpExporterOptions
    {
        public Func<Activity, bool> Filter { get; set; }
    }
}
