using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Elasticsearch.Net.Connection;
using Nest;

namespace Exceptionless.Core.Extensions {
    public static class ElasticSearchExtensions {
        private static readonly Lazy<PropertyInfo> _connectionSettingsProperty = new Lazy<PropertyInfo>(() => typeof(HttpConnection).GetProperty("ConnectionSettings", BindingFlags.NonPublic | BindingFlags.Instance));

        [Conditional("DEBUG")]
        public static void EnableTrace(this IElasticClient client) {
            if (!Settings.Current.EnableElasticsearchTracing)
                return;

            var conn = client.Connection as HttpConnection;
            if (conn == null)
                return;

            var settings = _connectionSettingsProperty.Value.GetValue(conn) as ConnectionSettings;
            settings?.EnableTrace();
        }

        [Conditional("DEBUG")]
        public static void DisableTrace(this IElasticClient client) {
            if (!Settings.Current.EnableElasticsearchTracing)
                return;

            var conn = client.Connection as HttpConnection;
            if (conn == null)
                return;

            var settings = _connectionSettingsProperty.Value.GetValue(conn) as ConnectionSettings;
            settings?.EnableTrace(false);
        }

        public static string GetErrorMessage(this IResponse response) {
            var sb = new StringBuilder();

            if (response.ConnectionStatus?.OriginalException != null)
                sb.AppendLine($"Original: ({response.ConnectionStatus.HttpStatusCode} - {response.ConnectionStatus.OriginalException.GetType().Name}) {response.ConnectionStatus.OriginalException.Message}");

            if (response.ServerError != null)
                sb.AppendLine($"Server: ({response.ServerError.Status} - {response.ServerError.ExceptionType}) {response.ServerError.Error}");
            
            if (sb.Length == 0)
                sb.AppendLine("Unknown error.");

            return sb.ToString();
        }
    }
}
