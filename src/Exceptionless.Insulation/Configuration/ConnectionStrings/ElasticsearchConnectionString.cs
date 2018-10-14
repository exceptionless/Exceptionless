using System;
using System.Collections.Generic;
using Exceptionless.Core;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation.Configuration.ConnectionStrings {
    public class ElasticsearchConnectionString : DefaultConnectionString, IElasticsearchConnectionString {
        public const string ProviderName = "elasticsearch";

        public ElasticsearchConnectionString(string connectionString, IDictionary<string, string> settings) : base(connectionString) {
            if (settings.TryGetValue("server", out string serverUrl) || settings.TryGetValue(String.Empty, out serverUrl))
                ServerUrl = serverUrl;

            if (settings.TryGetValue("shards", out string s) && Int32.TryParse(s, out int shards) && shards > 0)
                NumberOfShards = shards;

            if (settings.TryGetValue("replicas", out string r) && Int32.TryParse(r, out int replicas) && replicas >= 0)
                NumberOfReplicas = replicas;
            else
                NumberOfReplicas = AppOptions.Current.AppMode == AppMode.Production ? 1 : 0;

            if (settings.TryGetValue("field-limit", out string fl) && Int32.TryParse(fl, out int fieldsLimit) && fieldsLimit > 0)
                FieldsLimit = fieldsLimit;

            if (settings.TryGetValue("enable-size-plugin", out string value) && Boolean.TryParse(value, out bool enableSize))
                EnableMapperSizePlugin = enableSize;
            else
                EnableMapperSizePlugin = AppOptions.Current.AppMode != AppMode.Development;
        }

        public string ServerUrl { get; }
        public int NumberOfShards { get; } = 1;
        public int NumberOfReplicas { get; }
        public int FieldsLimit { get; } = 1500;
        public bool EnableMapperSizePlugin { get; }
    }
}