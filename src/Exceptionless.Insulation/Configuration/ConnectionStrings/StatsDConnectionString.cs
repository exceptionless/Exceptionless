using System;
using System.Collections.Generic;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation.Configuration.ConnectionStrings {
    public class StatsDConnectionString : DefaultConnectionString {
        public const string ProviderName = "statsd";

        public StatsDConnectionString(string connectionString, IDictionary<string, string> settings) : base(connectionString) {
            if (settings.TryGetValue("server", out string serverName) || settings.TryGetValue("server name", out serverName) || settings.TryGetValue("ServerName", out serverName) || settings.TryGetValue(String.Empty, out serverName))
                ServerName = serverName;

            if (settings.TryGetValue("port", out string serverPort) || settings.TryGetValue("server port", out serverPort) || settings.TryGetValue("ServerPort", out serverPort)) {
                if (Int32.TryParse(serverPort, out int port) && port > 0)
                    ServerPort = port;
            } else if(!String.IsNullOrEmpty(serverName)) {
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

        public string ServerName { get; } = "127.0.0.1";

        public int ServerPort { get; } = 8125;
    }
}
