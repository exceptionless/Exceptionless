using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Configuration {
    public class CacheOptions {
        public string ConnectionString { get; internal set; }
        public string Provider { get; internal set; }
        public Dictionary<string, string> Data { get; internal set; }

        public string Scope { get; internal set; }
        public string ScopePrefix { get; internal set; }
    }

    public class ConfigureCacheOptions : IConfigureOptions<CacheOptions> {
        private readonly IConfiguration _configuration;

        public ConfigureCacheOptions(IConfiguration configuration) {
            _configuration = configuration;
        }

        public void Configure(CacheOptions options) {
            options.Scope = _configuration.GetValue<string>(nameof(options.Scope), String.Empty);
            options.ScopePrefix = !String.IsNullOrEmpty(options.Scope) ? options.Scope + "-" : String.Empty;

            string cs = _configuration.GetConnectionString("Cache");
            options.Data = cs.ParseConnectionString();
            options.Provider = options.Data.GetString(nameof(options.Provider));

            var providerConnectionString = !String.IsNullOrEmpty(options.Provider) ? _configuration.GetConnectionString(options.Provider) : null;
            if (!String.IsNullOrEmpty(providerConnectionString))
                options.Data.AddRange(providerConnectionString.ParseConnectionString());

            options.ConnectionString = options.Data.BuildConnectionString(new HashSet<string> { nameof(options.Provider) });
        }
    }
}