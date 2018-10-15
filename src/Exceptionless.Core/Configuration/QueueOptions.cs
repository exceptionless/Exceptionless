using System;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Configuration {
    public class QueueOptions {
        public string ConnectionString { get; internal set; }
        public string Provider { get; internal set; }
        public Foundatio.Utility.DataDictionary Data { get; internal set; }

        public string Scope { get; internal set; }
        public string ScopePrefix { get; internal set; }
    }

    public class ConfigureQueueOptions : IConfigureOptions<QueueOptions> {
        private readonly IConfiguration _configuration;
        private readonly AppOptions _appOptions;

        public ConfigureQueueOptions(IConfiguration configuration, AppOptions appOptions) {
            _configuration = configuration;
            _appOptions = appOptions;
        }

        public void Configure(QueueOptions options) {
            string cs = _configuration.GetConnectionString("queue");
            options.Data = cs.ParseConnectionString();
            options.Provider = options.Data.GetString(nameof(options.Provider).ToLowerInvariant());
            options.ConnectionString = options.Data.GetString(nameof(options.ConnectionString).ToLowerInvariant());

            options.Scope = options.Data.GetString(nameof(options.Scope).ToLowerInvariant(), String.Empty);
            options.ScopePrefix = !String.IsNullOrEmpty(options.Scope) ? options.Scope + "-" : _appOptions.ScopePrefix;
        }
    }
}