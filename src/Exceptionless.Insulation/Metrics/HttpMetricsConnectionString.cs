using System;
using System.Collections.Generic;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation.Metrics {
    public class HttpMetricsConnectionString : IMetricsConnectionString {
        public HttpMetricsConnectionString(IDictionary<string, string> settings) {
            if ((settings.TryGetValue("server", out string serverUrl) || settings.TryGetValue("serverUrl", out serverUrl) || settings.TryGetValue("server url", out serverUrl) || settings.TryGetValue("network address", out serverUrl) || settings.TryGetValue("address", out serverUrl) || settings.TryGetValue("addr", out serverUrl) || settings.TryGetValue(String.Empty, out serverUrl)) && !String.IsNullOrEmpty(serverUrl)) {
                // Add the default scheme as http:// when it's lost.
                if (serverUrl.IndexOf("://", StringComparison.Ordinal) == -1) {
                    serverUrl = "http://" + serverUrl;
                }

                // Add the default port when it's lost.
                var defaultPort = DefaultPort;
                if (defaultPort.HasValue) {
                    if (serverUrl.IndexOf(':', serverUrl.IndexOf("://", StringComparison.Ordinal) + 3) == -1) {
                        serverUrl += ":" + defaultPort.Value;
                    }
                }

                var uriBuilder = new UriBuilder(serverUrl);
                if (!String.IsNullOrEmpty(uriBuilder.Password)) {
                    Password = uriBuilder.Password;
                    uriBuilder.Password = String.Empty;
                }

                if (!String.IsNullOrEmpty(uriBuilder.UserName)) {
                    UserName = uriBuilder.UserName;
                    uriBuilder.UserName = String.Empty;
                }

                ServerUrl = uriBuilder.Uri.ToString();
            }

            if (settings.TryGetValue("username", out string username) || settings.TryGetValue("user", out username) || settings.TryGetValue("userid", out username) || settings.TryGetValue("uid", out username)) {
                UserName = username;
            }

            if (settings.TryGetValue("password", out string password) || settings.TryGetValue("pwd", out password)) {
                Password = password;
            }
        }

        public string ServerUrl { get; }

        public string UserName { get; }

        public string Password { get; }

        protected virtual int? DefaultPort => null;
    }
}
