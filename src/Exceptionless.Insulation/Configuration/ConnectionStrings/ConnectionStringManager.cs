using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation.Configuration.ConnectionStrings {
    public static class ConnectionStringManager {
        public static void ParseAll(AppOptions appOptions) {
            appOptions.CacheConnectionString = Parse(appOptions.CacheConnectionString?.ConnectionString);
            appOptions.ElasticsearchConnectionString = Parse(appOptions.ElasticsearchConnectionString?.ConnectionString);
            appOptions.LdapConnectionString = Parse(appOptions.LdapConnectionString?.ConnectionString);
            appOptions.MessagingConnectionString = Parse(appOptions.MessagingConnectionString?.ConnectionString);
            appOptions.MetricsConnectionString = Parse(appOptions.MetricsConnectionString?.ConnectionString);
            appOptions.StorageConnectionString = Parse(appOptions.StorageConnectionString?.ConnectionString);
            appOptions.QueueConnectionString = Parse(appOptions.QueueConnectionString?.ConnectionString);
        }

        public static IConnectionString Parse(string connectionString) {
            if (String.IsNullOrEmpty(connectionString))
                return null;

            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string[] option in connectionString
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(kvp => kvp.Contains('='))
                .Select(kvp => kvp.Split(new[] { '=' }, 2))) {

                string optionKey = option[0]?.Trim();
                string optionValue = option[1]?.Trim();
                if (String.IsNullOrEmpty(optionValue))
                    options[String.Empty] = optionKey;
                else if (!String.IsNullOrEmpty(optionKey))
                    options[optionKey] = optionValue;
            }

            if (options.TryGetValue("provider", out string provider)) {
                switch (provider.ToLowerInvariant()) {
                    case AliyunConnectionString.ProviderName:
                        return new AliyunConnectionString(connectionString);
                    case AzureStorageConnectionString.ProviderName:
                        return new AzureStorageConnectionString(connectionString);
                    case ElasticsearchConnectionString.ProviderName:
                        return new ElasticsearchConnectionString(connectionString, options);
                    case FolderConnectionString.ProviderName:
                        return new FolderConnectionString(connectionString, options);
                    case GraphiteConnectionString.ProviderName:
                        return new GraphiteConnectionString(connectionString, options);
                    case HttpConnectionString.ProviderName:
                        return new HttpConnectionString(connectionString, options);
                    case InfluxDbConnectionString.ProviderName:
                        return new InfluxDbConnectionString(connectionString, options);
                    case MinioConnectionString.ProviderName:
                        return new MinioConnectionString(connectionString);
                    case LdapConnectionString.ProviderName:
                        return new LdapConnectionString(connectionString);
                    case PrometheusConnectionString.ProviderName:
                        return new PrometheusConnectionString(connectionString);
                    case RedisConnectionString.ProviderName:
                        return new RedisConnectionString(connectionString);
                    case StatsDConnectionString.ProviderName:
                        return new StatsDConnectionString(connectionString, options);
                    default:
                        throw new InvalidOperationException($"The provider '{provider}' cannot be recognized.");
                }
            }

            throw new InvalidOperationException("The 'provider' attribute is required in the connection string.");
        }
    }
}
