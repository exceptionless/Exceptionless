using System;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Configuration {
    public class CacheOptions {
        public string ConnectionString { get; internal set; }
    }

    public class ConfigureCacheOptions : IConfigureOptions<CacheOptions> {
        private readonly IConfiguration _configuration;
        private readonly AppOptions _appOptions;

        public ConfigureCacheOptions(IConfiguration configuration, AppOptions appOptions) {
            _configuration = configuration;
            _appOptions = appOptions;
        }

        public void Configure(CacheOptions options) {
            options.ConnectionString = _configuration.GetConnectionString("Cache");
        }
    }
}