using System;
using System.Collections.Generic;
using System.Linq;

namespace Exceptionless.Core.Utility {
    public interface IMetricsConnectionString {
    }

    public class StatsDMetricsConnectionString : IMetricsConnectionString {
        public StatsDMetricsConnectionString(IDictionary<String, String> settings) {
            String serverName, serverPort;
            if (settings.TryGetValue("server", out serverName) || settings.TryGetValue("server name", out serverName) || settings.TryGetValue("ServerName", out serverName) || settings.TryGetValue(String.Empty, out serverName)) {
                ServerName = serverName;
            }

            if (settings.TryGetValue("port", out serverPort) || settings.TryGetValue("server port", out serverPort) || settings.TryGetValue("ServerPort", out serverPort)) {
                if (int.TryParse(serverPort, out var port)) {
                    ServerPort = port;
                }
            }
            else {
                var colonIndex = serverName.IndexOf(':');
                if (colonIndex != -1) {
                    serverPort = serverName.Substring(colonIndex + 1);
                    if (int.TryParse(serverPort, out var port)) {
                        ServerPort = port;
                        ServerName = serverName.Substring(0, colonIndex);
                        return;
                    }
                }
            }
        }

        public String ServerName { get; }

        public int ServerPort { get; } = 8125;
    }

    public class HttpMetricsConnectionString : IMetricsConnectionString {
        public HttpMetricsConnectionString(IDictionary<String, String> settings) {
            String serverUrl, username, password;
            if (settings.TryGetValue("server", out serverUrl) || settings.TryGetValue("serverUrl", out serverUrl) || settings.TryGetValue("server url", out serverUrl) || settings.TryGetValue("network address", out serverUrl) || settings.TryGetValue("address", out serverUrl) || settings.TryGetValue("addr", out serverUrl) || settings.TryGetValue(String.Empty, out serverUrl)) {
                // Add the default scheme as http:// when it's lost.
                if (!String.IsNullOrEmpty(serverUrl) && serverUrl.IndexOf("://") == -1) {
                    serverUrl = "http://" + serverUrl;
                }

                // Add the default port when it's lost.
                var defaultPort = DefaultPort;
                if (defaultPort.HasValue) {
                    if (serverUrl.IndexOf(':', serverUrl.IndexOf("://") + 3) == -1) {
                        serverUrl += ":"+ defaultPort.Value;
                    }
                }

                var uriBuilder = new UriBuilder(serverUrl);
                if (!String.IsNullOrEmpty(uriBuilder.Password)) {
                    Password = uriBuilder.Password;
                    uriBuilder.Password = null;
                }

                if (!String.IsNullOrEmpty(uriBuilder.UserName)) {
                    UserName = uriBuilder.UserName;
                    uriBuilder.UserName = null;
                }

                ServerUrl = uriBuilder.Uri.ToString();
            }

            if (settings.TryGetValue("username", out username) || settings.TryGetValue("user", out username) || settings.TryGetValue("userid", out username) || settings.TryGetValue("uid", out username)) {
                UserName = username;
            }

            if (settings.TryGetValue("password", out password) || settings.TryGetValue("pwd", out password)) {
                Password = password;
            }
        }

        public String ServerUrl { get; }

        public String UserName { get; }

        public String Password { get; }

        protected virtual int? DefaultPort => null;
    }

    public class InfuxDBMetricsConnectionString : HttpMetricsConnectionString {
        public InfuxDBMetricsConnectionString(IDictionary<String, String> settings) : base(settings) {
            String database;
            if (settings.TryGetValue("database", out database) || settings.TryGetValue("catalog", out database)) {
                Database = database;
            }
        }

        public String Database { get; } = "exceptionless";

        protected override int? DefaultPort => 8086;
    }

    public class PrometheusMetricsConnectionString : IMetricsConnectionString { }

    public class GraphiteMetricsConnectionString : IMetricsConnectionString {
        public GraphiteMetricsConnectionString(IDictionary<String, String> settings) {
            string serverUrl;
            if (settings.TryGetValue("server", out serverUrl) || settings.TryGetValue("serverUrl", out serverUrl) || settings.TryGetValue("server url", out serverUrl) || settings.TryGetValue("network address", out serverUrl) || settings.TryGetValue("address", out serverUrl) || settings.TryGetValue("addr", out serverUrl) || settings.TryGetValue(String.Empty, out serverUrl)) {
                // Add the default scheme as http:// when it's lost.
                if (!String.IsNullOrEmpty(serverUrl)) {
                    if (serverUrl.IndexOf("://") == -1) {
                        serverUrl = "net.tcp://" + serverUrl;
                    }
                    // Add the default port as 8086 when it's lost.
                    if (serverUrl.IndexOf(':', serverUrl.IndexOf("://") + 3) == -1) {
                        serverUrl += ":2003";
                    }
                }
                ServerUrl = serverUrl;
            }
        }

        public String ServerUrl { get; }
    }

    public static class MetricsConnectionString {
        public static IMetricsConnectionString Parse(String connectionString) {
            if (string.IsNullOrEmpty(connectionString)) return null;
            var options = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            foreach (var option in connectionString
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(kvp => kvp.Contains('='))
                .Select(kvp => kvp.Split(new[] { '=' }, 2))) {
                var optionKey = option[0].Trim();
                var optionValue = option[1].Trim();
                if (String.IsNullOrEmpty(optionValue)) {
                    options[String.Empty] = optionKey;
                }
                else if(!String.IsNullOrEmpty(optionKey)) {
                    options[optionKey] = optionValue;
                }
            }

            string provider;
            if (options.TryGetValue("provider", out provider) || options.TryGetValue("reporter", out provider)) {
                if (String.Equals(provider, "statsd", StringComparison.OrdinalIgnoreCase)) {
                    return new StatsDMetricsConnectionString(options);
                }

                if (String.Equals(provider, "http", StringComparison.OrdinalIgnoreCase)) {
                    return new HttpMetricsConnectionString(options);
                }

                if (String.Equals(provider, "infuxdb", StringComparison.OrdinalIgnoreCase)) {
                    return new InfuxDBMetricsConnectionString(options);
                }

                if (String.Equals(provider, "prometheus", StringComparison.OrdinalIgnoreCase)) {
                    return new PrometheusMetricsConnectionString();
                }

                if (String.Equals(provider, "graphite", StringComparison.OrdinalIgnoreCase)) {
                    return new GraphiteMetricsConnectionString(options);
                }

                throw new InvalidOperationException($"The metrics provider {provider} cannot be recoganized.");
            }

            throw new InvalidOperationException("The metrics provider is required in the connection string.");
        }
    }
}
