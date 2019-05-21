using System;
using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Configuration {
    public class ElasticsearchOptions {
        public string ServerUrl { get; internal set; }
        public int NumberOfShards { get; internal set; } = 1;
        public int NumberOfReplicas { get; internal set; }
        public int FieldsLimit { get; internal set; } = 1500;
        public bool EnableMapperSizePlugin { get; internal set; }

        public string Scope { get; internal set; }
        public string ScopePrefix { get; internal set; }

        public bool EnableSnapshotJobs { get; set; }
        public bool DisableIndexConfiguration { get; set; }

        public string Password { get; internal set; }
        public string UserName { get; internal set; }
    }

    public class ConfigureElasticsearchOptions : IConfigureOptions<ElasticsearchOptions> {
        private readonly IConfiguration _configuration;
        private readonly IOptions<AppOptions> _appOptions;

        public ConfigureElasticsearchOptions(IConfiguration configuration, IOptions<AppOptions> appOptions) {
            _configuration = configuration;
            _appOptions = appOptions;
        }

        public void Configure(ElasticsearchOptions options) {
            options.Scope = _configuration.GetValue<string>(nameof(options.Scope), String.Empty);
            options.ScopePrefix = !String.IsNullOrEmpty(options.Scope) ? options.Scope + "-" : String.Empty;

            options.DisableIndexConfiguration = _configuration.GetValue(nameof(options.DisableIndexConfiguration), false);
            options.EnableSnapshotJobs = _configuration.GetValue(nameof(options.EnableSnapshotJobs), String.IsNullOrEmpty(options.ScopePrefix) && _appOptions.Value.AppMode == AppMode.Production);

            string connectionString = _configuration.GetConnectionString("Elasticsearch");
            var pairs = connectionString.ParseConnectionString();

            options.ServerUrl = pairs.GetString("server", "http://localhost:9200");

            int shards = pairs.GetValueOrDefault<int>("shards", 1);
            options.NumberOfShards = shards > 0 ? shards : 1;

            int replicas = pairs.GetValueOrDefault<int>("replicas", _appOptions.Value.AppMode == AppMode.Production ? 1 : 0);
            options.NumberOfReplicas = replicas > 0 ? replicas : 0;

            int fieldsLimit = pairs.GetValueOrDefault("field-limit", 1500);
            options.FieldsLimit = fieldsLimit > 0 ? fieldsLimit : 1500;

            options.EnableMapperSizePlugin = pairs.GetValueOrDefault("enable-size-plugin", _appOptions.Value.AppMode != AppMode.Development);

            options.UserName = pairs.GetString("username");
            options.Password = pairs.GetString("password");
        }
    }
}