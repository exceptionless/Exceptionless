using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration {
    public class MetricOptions {
        public string ConnectionString { get; internal set; }
        public string Provider { get; internal set; }
        public Dictionary<string, string> Data { get; internal set; }

        public static MetricOptions ReadFromConfiguration(IConfiguration config) {
            var options = new MetricOptions();

            string cs = config.GetConnectionString("Metrics");
            options.Data = cs.ParseConnectionString();
            options.Provider = options.Data.GetString(nameof(options.Provider));

            var providerConnectionString = !String.IsNullOrEmpty(options.Provider) ? config.GetConnectionString(options.Provider) : null;
            if (!String.IsNullOrEmpty(providerConnectionString))
                options.Data.AddRange(providerConnectionString.ParseConnectionString());

            options.ConnectionString = options.Data.BuildConnectionString(new HashSet<string> { nameof(options.Provider) });

            return options;
        }
    }
}