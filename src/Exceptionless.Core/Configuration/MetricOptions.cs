using System;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Configuration {
    public class MetricOptions {
        public string ConnectionString { get; internal set; }
        public bool EnableMetricsReporting { get; internal set; }
    }

    public class ConfigureMetricOptions : IConfigureOptions<MetricOptions> {
        private readonly IConfiguration _configuration;
        private readonly AppOptions _appOptions;

        public ConfigureMetricOptions(IConfiguration configuration, AppOptions appOptions) {
            _configuration = configuration;
            _appOptions = appOptions;
        }

        public void Configure(MetricOptions options) {
            options.ConnectionString = _configuration.GetConnectionString("Metric");
            options.EnableMetricsReporting = _configuration.GetValue(nameof(options.EnableMetricsReporting), options.ConnectionString != null);

        }
    }
}