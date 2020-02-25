using System;
using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;

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
        public DateTime ReindexCutOffDate { get; internal set; }
        public ElasticsearchOptions ElasticsearchToMigrate { get; internal set; }

        public static ElasticsearchOptions ReadFromConfiguration(IConfiguration config, AppOptions appOptions) {
            var options = new ElasticsearchOptions();

            options.Scope = appOptions.AppScope;
            options.ScopePrefix = !String.IsNullOrEmpty(options.Scope) ? options.Scope + "-" : String.Empty;

            options.DisableIndexConfiguration = config.GetValue(nameof(options.DisableIndexConfiguration), false);
            options.EnableSnapshotJobs = config.GetValue(nameof(options.EnableSnapshotJobs), String.IsNullOrEmpty(options.ScopePrefix) && appOptions.AppMode == AppMode.Production);
            options.ReindexCutOffDate = config.GetValue(nameof(options.ReindexCutOffDate), DateTime.MinValue);

            string connectionString = config.GetConnectionString("Elasticsearch");
            ParseConnectionString(connectionString, options, appOptions.AppMode);

            string connectionStringToMigrate = config.GetConnectionString("ElasticsearchToMigrate");
            if (!String.IsNullOrEmpty(connectionStringToMigrate)) {
                options.ElasticsearchToMigrate = new ElasticsearchOptions {
                    ReindexCutOffDate = options.ReindexCutOffDate
                };

                ParseConnectionString(connectionStringToMigrate, options.ElasticsearchToMigrate, appOptions.AppMode);
            }

            return options;
        }

        private static void ParseConnectionString(string connectionString, ElasticsearchOptions options, AppMode appMode) {
            var pairs = connectionString.ParseConnectionString();

            options.ServerUrl = pairs.GetString("server", "http://localhost:9200");

            int shards = pairs.GetValueOrDefault<int>("shards", 1);
            options.NumberOfShards = shards > 0 ? shards : 1;

            int replicas = pairs.GetValueOrDefault<int>("replicas", appMode == AppMode.Production ? 1 : 0);
            options.NumberOfReplicas = replicas > 0 ? replicas : 0;

            int fieldsLimit = pairs.GetValueOrDefault("field-limit", 1500);
            options.FieldsLimit = fieldsLimit > 0 ? fieldsLimit : 1500;

            options.EnableMapperSizePlugin = pairs.GetValueOrDefault("enable-size-plugin", appMode != AppMode.Development);

            options.UserName = pairs.GetString("username");
            options.Password = pairs.GetString("password");

            string scope = pairs.GetString(nameof(options.Scope).ToLowerInvariant());
            if (!String.IsNullOrEmpty(scope))
                options.Scope = scope;
        }
    }
}