using System;
using System.Diagnostics;
using System.Reflection;
using Elasticsearch.Net.Connection;
using Nest;

namespace Exceptionless.Core.Extensions {
    public static class ElasticSearchExtensions {
        private static readonly Lazy<PropertyInfo> _connectionSettingsProperty = new Lazy<PropertyInfo>(() => typeof(HttpConnection).GetProperty("ConnectionSettings", BindingFlags.NonPublic | BindingFlags.Instance));

        [Conditional("TRACE")]
        public static void EnableTrace(this IElasticClient client) {
            var conn = client.Connection as HttpConnection;
            if (conn == null)
                return;

            var settings = _connectionSettingsProperty.Value.GetValue(conn) as ConnectionSettings;
            if (settings != null)
                settings.EnableTrace();
        }

        [Conditional("TRACE")]
        public static void DisableTrace(this IElasticClient client) {
            var conn = client.Connection as HttpConnection;
            if (conn == null)
                return;

            var settings = _connectionSettingsProperty.Value.GetValue(conn) as ConnectionSettings;
            if (settings != null)
                settings.EnableTrace(false);
        }
    }
}
