using System.Diagnostics.Tracing;
using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using Serilog;
using System.Net;

namespace OpenTelemetry {
    public static class ApmExtensions {
        public static IHostBuilder AddApm(this IHostBuilder builder, ApmConfig config) {
            string apiKey = config.ApiKey;
            if (!String.IsNullOrEmpty(apiKey) && apiKey.Length > 6)
                apiKey = String.Concat(apiKey.AsSpan(0, 6), "***");

            Log.Information("Configuring APM: Endpoint={Endpoint} Insecure={Insecure} ApiKey={ApiKey} EnableTracing={Enabled} EnableLogs={EnableLogs} FullDetails={FullDetails} EnableRedis={EnableRedis} SampleRate={SampleRate}",
                config.Endpoint, config.Insecure, apiKey, config.EnableTracing, config.EnableLogs, config.FullDetails, config.EnableRedis, config.SampleRate);

            if (config.Insecure) {
                ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, errors) => true;
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            }

            var resourceBuilder = ResourceBuilder.CreateDefault().AddService(config.ServiceName).AddAttributes(new[] {
                new KeyValuePair<string, object>("service.namespace", config.ServiceNamespace),
                new KeyValuePair<string, object>("service.environment", config.ServiceEnvironment),
                new KeyValuePair<string, object>("service.version", config.ServiceVersion)
            });
            
            builder.ConfigureServices(services => {
                services.AddHostedService(sp => new SelfDiagnosticsLoggingHostedService(sp.GetRequiredService<ILoggerFactory>(), config.Debug ? EventLevel.Verbose : null));

                if (config.EnableTracing)
                    services.AddOpenTelemetryTracing(b => {
                        b.SetResourceBuilder(resourceBuilder);

                        b.AddAspNetCoreInstrumentation(o => {
                            o.Filter = context => {
                                return !context.Request.Headers.UserAgent.ToString().Contains("HealthChecker");
                            };
                        });

                        b.AddElasticsearchClientInstrumentation(c => {
                            c.SuppressDownstreamInstrumentation = true;
                            c.ParseAndFormatRequest = config.FullDetails;
                            c.Enrich = (activity, source, data) => {
                                // truncate statements to 4096 length
                                var dbStatement = activity.GetTagItem("db.statement") as string;
                                if (dbStatement != null && dbStatement.Length > 4096)
                                    activity.SetTag("db.statement", dbStatement.Substring(0, 4096));

                                // 404s should not be error
                                var httpStatus = activity.GetTagItem("http.status_code") as int?;
                                if (httpStatus.HasValue && httpStatus.Value == 404)
                                    activity.SetStatus(Status.Unset);
                            };
                        });

                        b.AddHttpClientInstrumentation();
                        b.AddSource("Exceptionless", "Foundatio");

                        if (config.EnableRedis)
                            b.AddRedisInstrumentation(null, c => {
                                c.SetVerboseDatabaseStatements = config.FullDetails;
                            });

                        b.SetSampler(new TraceIdRatioBasedSampler(config.SampleRate));

                        if (config.Console)
                            b.AddConsoleExporter();

                        if (!String.IsNullOrEmpty(config.Endpoint)) {
                            b.AddOtlpExporter(c => {
                                if (!String.IsNullOrEmpty(config.Endpoint))
                                    c.Endpoint = new Uri(config.Endpoint);
                                if (!String.IsNullOrEmpty(config.ApiKey))
                                    c.Headers = $"api-key={config.ApiKey}";
                            });
                        }
                    });

                services.AddOpenTelemetryMetrics(b => {
                    b.SetResourceBuilder(resourceBuilder);

                    b.AddHttpClientInstrumentation();
                    b.AddAspNetCoreInstrumentation();
                    b.AddMeter("Exceptionless", "Foundatio");
                    b.AddRuntimeMetrics();

                    if (config.Console)
                        b.AddConsoleExporter((exporterOptions, metricReaderOptions) => {
                            // The ConsoleMetricExporter defaults to a manual collect cycle.
                            // This configuration causes metrics to be exported to stdout on a 10s interval.
                            metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 10000;
                        });

                    b.AddPrometheusExporter();

                    if (!String.IsNullOrEmpty(config.Endpoint))
                        b.AddOtlpExporter((c, o) => {
                            // needed for newrelic compatibility until they support cumulative
                            o.TemporalityPreference = MetricReaderTemporalityPreference.Delta;

                            if (!String.IsNullOrEmpty(config.Endpoint))
                                c.Endpoint = new Uri(config.Endpoint);
                            if (!String.IsNullOrEmpty(config.ApiKey))
                                c.Headers = $"api-key={config.ApiKey}";
                        });
                });

                if (config.EnableLogs) {
                    services.AddSingleton<ILoggerProvider, OpenTelemetryLoggerProvider>();
                    services.Configure<OpenTelemetryLoggerOptions>(o => {
                        o.SetResourceBuilder(resourceBuilder);
                        o.IncludeScopes = true;
                        o.ParseStateValues = true;
                        o.IncludeFormattedMessage = true;

                        if (config.Console)
                            o.AddConsoleExporter();

                        if (!String.IsNullOrEmpty(config.Endpoint)) {
                            o.AddOtlpExporter(c => {
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
    }

    public class ApmConfig {
        private readonly IConfiguration _apmConfig;

        public ApmConfig(IConfigurationRoot config, string processName, string serviceVersion, bool enableRedis) {
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

        public bool EnableTracing => _apmConfig.GetValue("EnableTracing", false);
        public bool Insecure { get; set; }
        public string ServiceName { get; }
        public string ServiceEnvironment { get; }
        public string ServiceNamespace { get; }
        public string ServiceVersion { get; }
        public string Endpoint => _apmConfig.GetValue("Endpoint", String.Empty);
        public string ApiKey => _apmConfig.GetValue("ApiKey", String.Empty);
        public bool FullDetails => _apmConfig.GetValue("FullDetails", false);
        public bool EnableLogs => _apmConfig.GetValue("EnableLogs", false);
        public double SampleRate => _apmConfig.GetValue("SampleRate", 1.0);
        public bool EnableRedis { get; }
        public bool Debug => _apmConfig.GetValue("Debug", false);
        public bool Console => _apmConfig.GetValue("Console", false);
    }
}