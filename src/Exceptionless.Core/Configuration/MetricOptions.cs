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

        public static MetricOptions ReadFromConfiguration(IConfiguration config) {
            var options = new MetricOptions();
            var configureOptions = new ConfigureMetricOptions(config);
            configureOptions.Configure(options);
            return options;
        }
    }

    public class ConfigureMetricOptions : IConfigureOptions<MetricOptions> {
        private readonly IConfiguration _configuration;

        public ConfigureMetricOptions(IConfiguration configuration) {
            _configuration = configuration;
        }

        public void Configure(MetricOptions options) {
            string cs = _configuration.GetConnectionString("Metrics");
            options.Data = cs.ParseConnectionString();
            options.Provider = options.Data.GetString(nameof(options.Provider));
            
            var providerConnectionString = !String.IsNullOrEmpty(options.Provider) ? _configuration.GetConnectionString(options.Provider) : null;
            if (!String.IsNullOrEmpty(providerConnectionString))
                options.Data.AddRange(providerConnectionString.ParseConnectionString());
            
            options.ConnectionString = options.Data.BuildConnectionString(new HashSet<string> { nameof(options.Provider) });
        }
    }
}