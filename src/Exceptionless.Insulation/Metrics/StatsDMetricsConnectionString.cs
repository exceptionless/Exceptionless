using System;
using System.Collections.Generic;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation.Metrics {
    public class StatsDMetricsConnectionString : IMetricsConnectionString {
        public StatsDMetricsConnectionString(IDictionary<string, string> settings) {
            if (settings.TryGetValue("server", out string serverName) || settings.TryGetValue("server name", out serverName) || settings.TryGetValue("ServerName", out serverName) || settings.TryGetValue(String.Empty, out serverName)) {
                ServerName = serverName;
            }

            if (settings.TryGetValue("port", out string serverPort) || settings.TryGetValue("server port", out serverPort) || settings.TryGetValue("ServerPort", out serverPort)) {
                if (int.TryParse(serverPort, out int port)) {
                    ServerPort = port;
                }
            }
            else if(!String.IsNullOrEmpty(serverName)) {
                int colonIndex = serverName.IndexOf(':');
                if (colonIndex != -1) {
                    serverPort = serverName.Substring(colonIndex + 1);
                    if (int.TryParse(serverPort, out int port)) {
                        ServerPort = port;
                        ServerName = serverName.Substring(0, colonIndex);
                    }
                }
            }
        }

        public string ServerName { get; }

        public int ServerPort { get; } = 8125;
    }
}
