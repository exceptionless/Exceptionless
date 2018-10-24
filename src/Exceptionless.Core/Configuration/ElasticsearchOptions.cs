using System;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Configuration {
    public class ElasticsearchOptions {
        public string ServerUrl { get; internal set; }
        public int NumberOfShards { get; internal set; } = 1;
        public int NumberOfReplicas { get; internal set; }
        public int FieldsLimit { get; internal set; } = 1500;
        public bool EnableMapperSizePlugin { get; internal set; }
    }

    public class ConfigureElasticsearchOptions : IConfigureOptions<ElasticsearchOptions> {
        private readonly IConfiguration _configuration;
        private readonly IOptionsSnapshot<AppOptions> _appOptions;

        public ConfigureElasticsearchOptions(IConfiguration configuration, IOptionsSnapshot<AppOptions> appOptions) {
            _configuration = configuration;
            _appOptions = appOptions;
        }

        public void Configure(ElasticsearchOptions options) {
            string connectionString = _configuration.GetConnectionString("elasticsearch");
            var pairs = connectionString.ParseConnectionString();

            options.ServerUrl = pairs.GetString("server", "http://localhost:9200");

            int shards = pairs.GetValueOrDefault<int>("shards", 1);
            options.NumberOfShards = shards > 0 ? shards : 1;

            int replicas = pairs.GetValueOrDefault<int>("replicas", _appOptions.Value.AppMode == AppMode.Production ? 1 : 0);
            options.NumberOfReplicas = replicas > 0 ? replicas : 0;

            int fieldsLimit = pairs.GetValueOrDefault("field-limit", 1500);
            options.FieldsLimit = fieldsLimit > 0 ? fieldsLimit : 1500;

            options.EnableMapperSizePlugin = pairs.GetValueOrDefault("enable-size-plugin", _appOptions.Value.AppMode != AppMode.Development);
        }
    }
}