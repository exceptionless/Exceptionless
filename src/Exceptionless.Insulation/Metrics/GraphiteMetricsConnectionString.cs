using System;
using System.Collections.Generic;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation.Metrics {
    public class GraphiteMetricsConnectionString : DefaultMetricsConnectionString {
        public GraphiteMetricsConnectionString(string connectionString, IDictionary<string, string> settings) : base(connectionString) {
            if (settings.TryGetValue("server", out string serverUrl) || settings.TryGetValue("serverUrl", out serverUrl) || settings.TryGetValue("server url", out serverUrl) || settings.TryGetValue("network address", out serverUrl) || settings.TryGetValue("address", out serverUrl) || settings.TryGetValue("addr", out serverUrl) || settings.TryGetValue(String.Empty, out serverUrl)) {
                // Add the default scheme as http:// when it's lost.
                if (!String.IsNullOrEmpty(serverUrl)) {
                    if (serverUrl.IndexOf("://", StringComparison.Ordinal) == -1) {
                        serverUrl = "net.tcp://" + serverUrl;
                    }
                    // Add the default port as 8086 when it's lost.
                    if (serverUrl.IndexOf(':', serverUrl.IndexOf("://", StringComparison.Ordinal) + 3) == -1) {
                        serverUrl += ":2003";
                    }
                }
                ServerUrl = serverUrl;
            }
        }

        public String ServerUrl { get; }
    }
}
