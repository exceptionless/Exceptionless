using System;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Configuration {
    public class CacheOptions {
        public string ConnectionString { get; internal set; }
        public string Provider { get; internal set; }
        public Foundatio.Utility.DataDictionary Data { get; internal set; }
    }

    public class ConfigureCacheOptions : IConfigureOptions<CacheOptions> {
        private readonly IConfiguration _configuration;

        public ConfigureCacheOptions(IConfiguration configuration) {
            _configuration = configuration;
        }

        public void Configure(CacheOptions options) {
            string cs = _configuration.GetConnectionString("cache");
            options.Data = cs.ParseConnectionString();
            options.Provider = options.Data.GetString(nameof(options.Provider).ToLowerInvariant());
            options.ConnectionString = options.Data.GetString(nameof(options.ConnectionString).ToLowerInvariant());
        }
    }
}