using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace OpenTelemetry {
    public static class ApmExtensions {
        public static IServiceCollection AddApm(this IServiceCollection services, ApmConfig config) {
            if (!config.Enabled)
                return services;

            string apiKey = config.ApiKey;
            if (!String.IsNullOrEmpty(apiKey) && apiKey.Length > 6)
                apiKey = apiKey.Substring(0, 6) + "***";

            Log.Information("Configuring APM: Endpoint={Endpoint} ApiKey={ApiKey} FullDetails={FullDetails} EnableRedis={EnableRedis} SampleRate={SampleRate}",
                config.Endpoint, apiKey, config.FullDetails, config.EnableRedis, config.SampleRate);

            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            services.AddHostedService(sp => new SelfDiagnosticsLoggingHostedService(sp.GetRequiredService<ILoggerFactory>(), config.Debug ? EventLevel.Verbose : null));

            services.AddOpenTelemetryTracing(b => {
                b.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(config.ServiceName).AddAttributes(new[] {
                    new KeyValuePair<string, object>("service.namespace", config.ServiceNamespace),
                    new KeyValuePair<string, object>("service.version", config.ServiceVersion)
                }));

                b.AddAspNetCoreInstrumentation();
                b.AddElasticsearchClientInstrumentation(c => {
                    c.SuppressDownstreamInstrumentation = true;
                    c.ParseAndFormatRequest = config.FullDetails;
                    c.Enrich = (activity, source, data) => {
                        activity.SetTag("service.name", "Elasticsearch");
                        if (activity.DisplayName.StartsWith("Elasticsearch "))
                            activity.DisplayName = activity.DisplayName.Substring(14);
                    };
                });
                b.AddHttpClientInstrumentation(o => {
                    o.Enrich = (activity, source, data) => {
                        if (data is HttpRequestMessage request) {
                            if (request.RequestUri.Host.EndsWith("amazonaws.com")) {
                                if (request.RequestUri.Host.StartsWith("sqs"))
                                    activity.SetTag("service.name", "AWS SQS");
                                else if (request.RequestUri.Host.Contains("s3"))
                                    activity.SetTag("service.name", "AWS S3");
                                else
                                    activity.SetTag("service.name", "AWS");
                            } else {
                                activity.SetTag("service.name", "External HTTP");
                            }
                        }
                    };
                });
                b.AddSource("Foundatio");
                if (config.EnableRedis)
                    b.AddRedisInstrumentation(null, c => {
                        c.SetCommandKey = config.FullDetails;
                        c.Enrich = (activity, command) => {
                            activity.SetTag("service.name", "Redis");
                        };
                    });

                b.SetSampler(new TraceIdRatioBasedSampler(config.SampleRate));
                b.AddOtlpExporter(c => {
                    if (!String.IsNullOrEmpty(config.Endpoint))
                        c.Endpoint = new Uri(config.Endpoint);
                    if (!String.IsNullOrEmpty(config.ApiKey))
                        c.Headers = $"api-key={config.ApiKey}";
                });
            });

            //services.Configure<AspNetCoreInstrumentationOptions>(options => {
            //    options.Filter = (req) => {
            //        return req.Request.Host != null;
            //    };
            //});

            return services;
        }
    }

    public class ApmConfig {
        private readonly IConfiguration config;

        public ApmConfig(IConfigurationRoot config, string serviceName, string serviceNamespace, string serviceVersion, bool enableRedis) {
            this.config = config.GetSection("Apm");
            ServiceName = serviceName;
            ServiceNamespace = serviceNamespace;
            ServiceVersion = serviceVersion;
            EnableRedis = enableRedis;
        }

        public bool Enabled => config.GetValue("Enabled", false);
        public string ServiceName { get; }
        public string ServiceNamespace { get; }
        public string ServiceVersion { get; }
        public string Endpoint => config.GetValue("Endpoint", String.Empty);
        public string ApiKey => config.GetValue("ApiKey", String.Empty);
        public bool FullDetails => config.GetValue("FullDetails", false);
        public double SampleRate => config.GetValue("SampleRate", 1.0);
        public bool EnableRedis { get; }
        public bool Debug => config.GetValue("Debug", false);
    }
}
