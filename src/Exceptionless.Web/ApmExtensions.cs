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
            { "service.environment", config.ServiceEnvironment }
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
                b.AddProcessor<ElasticCompatibilityProcessor>();
                b.SetResourceBuilder(resourceBuilder);

                b.AddAspNetCoreInstrumentation(o =>
                {
                    o.Filter = context => !context.Request.Headers.UserAgent.ToString().Contains("HealthChecker");
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
                        int? httpStatus = activity.GetTagItem("http.status_code") as int?;
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

                //b.SetSampler(new TraceIdRatioBasedSampler(config.SampleRate));

                if (config.Console)
                    b.AddConsoleExporter();

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
        _apmConfig = config.GetSection("Apm");
        processName = processName.StartsWith('-') ? processName : "-" + processName;

        ServiceName = _apmConfig.GetValue("ServiceName", "") + processName;
        if (ServiceName.StartsWith('-'))
            ServiceName = ServiceName.Substring(1);

        ServiceEnvironment = _apmConfig.GetValue("ServiceEnvironment", "") ?? throw new InvalidOperationException();
        ServiceNamespace = _apmConfig.GetValue("ServiceNamespace", ServiceName) ?? throw new InvalidOperationException();
        ServiceVersion = serviceVersion;
        EnableRedis = enableRedis;
    }

    public bool EnableLogs => _apmConfig.GetValue("EnableLogs", false);
    public bool Insecure => _apmConfig.GetValue("Insecure", false);
    public string SslThumbprint => _apmConfig.GetValue("SslThumbprint", String.Empty) ?? throw new InvalidOperationException();
    public string ServiceName { get; }
    public string ServiceEnvironment { get; }
    public string ServiceNamespace { get; }
    public string? ServiceVersion { get; }
    public bool FullDetails => _apmConfig.GetValue("FullDetails", false);
    public int MinDurationMs => _apmConfig.GetValue<int>("MinDurationMs", -1);
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

public class ElasticCompatibilityProcessor : BaseProcessor<Activity>
{
    private readonly AsyncLocal<ActivitySpanId?> _currentTransactionId = new();
    public const string TransactionIdTagName = "transaction.id";

	public override void OnEnd(Activity activity)
	{
        if (activity.Parent == null)
            _currentTransactionId.Value = activity.SpanId;

        if (_currentTransactionId.Value.HasValue)
            activity.SetTag(TransactionIdTagName, _currentTransactionId.Value.Value.ToString());

        if (activity.Kind == ActivityKind.Server)
		{
			string? httpScheme = null;
			string? httpTarget = null;
			string? urlScheme = null;
			string? urlPath = null;
			string? urlQuery = null;
			string? netHostName = null;
			int? netHostPort = null;
			string? serverAddress = null;
			int? serverPort = null;

			foreach (var tag in activity.TagObjects)
			{
				if (tag.Key == TraceSemanticConventions.HttpScheme)
					httpScheme = ProcessStringAttribute(tag);

				if (tag.Key == TraceSemanticConventions.HttpTarget)
					httpTarget = ProcessStringAttribute(tag);

				if (tag.Key == TraceSemanticConventions.UrlScheme)
					urlScheme = ProcessStringAttribute(tag);

				if (tag.Key == TraceSemanticConventions.UrlPath)
					urlPath = ProcessStringAttribute(tag);

				if (tag.Key == TraceSemanticConventions.UrlQuery)
					urlQuery = ProcessStringAttribute(tag);

				if (tag.Key == TraceSemanticConventions.NetHostName)
					netHostName = ProcessStringAttribute(tag);

				if (tag.Key == TraceSemanticConventions.ServerAddress)
					serverAddress = ProcessStringAttribute(tag);

				if (tag.Key == TraceSemanticConventions.NetHostPort)
					netHostPort = ProcessIntAttribute(tag);

				if (tag.Key == TraceSemanticConventions.ServerPort)
					serverPort = ProcessIntAttribute(tag);
			}

			// Set the older semantic convention attributes
			if (httpScheme is null && urlScheme is not null)
				SetStringAttribute(TraceSemanticConventions.HttpScheme, urlScheme);

			if (httpTarget is null && urlPath is not null)
			{
				var target = urlPath;

				if (urlQuery is not null)
					target += $"?{urlQuery}";

				SetStringAttribute(TraceSemanticConventions.HttpTarget, target);
			}

			if (netHostName is null && serverAddress is not null)
				SetStringAttribute(TraceSemanticConventions.NetHostName, serverAddress);

			if (netHostPort is null && serverPort is not null)
				SetIntAttribute(TraceSemanticConventions.NetHostPort, serverPort.Value);
		}

		string? ProcessStringAttribute(KeyValuePair<string, object?> tag)
		{
			if (tag.Value is string value)
			{
				return value;
			}

			return null;
		}

		int? ProcessIntAttribute(KeyValuePair<string, object?> tag)
		{
			if (tag.Value is int value)
			{
				return value;
			}

			return null;
		}

		void SetStringAttribute(string attributeName, string value)
		{
			activity.SetTag(attributeName, value);
		}

		void SetIntAttribute(string attributeName, int value)
		{
			activity.SetTag(attributeName, value);
		}
	}
}

internal static class TraceSemanticConventions
{
    // HTTP
    public const string HttpScheme = "http.scheme";
    public const string HttpTarget = "http.target";

    // NET
    public const string NetHostName = "net.host.name";
    public const string NetHostPort = "net.host.port";

    // SERVER
    public const string ServerAddress = "server.address";
    public const string ServerPort = "server.port";

    // URL
    public const string UrlPath = "url.path";
    public const string UrlQuery = "url.query";
    public const string UrlScheme = "url.scheme";
}
