using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Configuration {
    public class MetricOptions {
        public string ConnectionString { get; internal set; }
        public string Provider { get; internal set; }
        public Dictionary<string, string> Data { get; internal set; }
    }

    public class ConfigureMetricOptions : IConfigureOptions<MetricOptions> {
        private readonly IConfiguration _configuration;

        public ConfigureMetricOptions(IConfiguration configuration) {
            _configuration = configuration;
        }

        public void Configure(MetricOptions options) {
            string cs = _configuration.GetConnectionString("metric");
            options.Data = cs.ParseConnectionString();
            options.Provider = options.Data.GetString(nameof(options.Provider).ToLowerInvariant());
            options.ConnectionString = options.Data.BuildConnectionString(new HashSet<string> { nameof(options.Provider).ToLowerInvariant() });
        }
    }
}