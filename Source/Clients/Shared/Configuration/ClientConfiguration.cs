#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Threading;
using Exceptionless.Extensions;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Utility;

namespace Exceptionless.Configuration {
    public class ClientConfiguration {
        internal const string CachedServerConfigFile = "Server.config";
        private readonly Dictionary<string, string> _dictionary = new Dictionary<string, string>();

        internal ClientConfiguration() {
            Enabled = true;
        }

        public string this[string key] { get { return _dictionary[key]; } internal set { _dictionary[key] = value; } }

        public bool ContainsKey(string key) {
            return _dictionary.ContainsKey(key);
        }

        public ICollection<string> Keys { get { return _dictionary.Keys; } }

        private string _storeId;

        public string StoreId {
            get {
                if (String.IsNullOrEmpty(_storeId) && HasValidApiKey)
                    _storeId = ApiKey.Substring(0, 8);

                return _storeId ?? "Exceptionless";
            }
        }

        public string ServerUrl { get; internal set; }

        public string QueuePath { get; internal set; }

        public string ApiKey { get; internal set; }

        internal bool TestMode { get; set; }

        internal bool HasValidApiKey {
            get {
                string key = ApiKey != null ? ApiKey.Trim() : null;
                return !String.IsNullOrEmpty(key)
                       && key.Length >= 10
                       && !key.Contains(" ")
                       && !String.Equals(key, "API_KEY_HERE", StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool Enabled { get; internal set; }

        public bool EnableSSL { get; internal set; }

        public bool EnableLogging { get; internal set; }

        public string LogPath { get; internal set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include private information about the local machine.
        /// </summary>
        /// <value>
        /// <c>true</c> to include private information about the local machine; otherwise, <c>false</c>.
        /// </value>
        public bool IncludePrivateInformation { get; set; }

        public int TraceLogLimit { get { return _dictionary.GetInt32(TraceLogLimitKey, 0); } }

        public string SupportEmail { get { return _dictionary.GetString(SupportEmailKey, String.Empty); } }

        /// <summary>
        /// A comma delimited list of field names that should be removed from any error report data.
        /// For example, entering CreditCard will remove any extended data properties, form fields, cookies and query
        /// parameters from the report.
        /// </summary>
        public ICollection<string> DataExclusions { get { return GetStringCollection("@@DataExclusions", null); } }

        private ICollection<string> GetStringCollection(string name, string @default) {
            string value = _dictionary.GetString(name, @default);

            if (String.IsNullOrEmpty(value))
                return new List<string>().AsReadOnly();

            string[] values = value.Split(
                                          new[] { ",", ";", Environment.NewLine },
                StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < values.Length; i++)
                values[i] = values[i].Trim();

            var list = new List<string>(values);
            return list.AsReadOnly();
        }

        internal static void ProcessServerConfigResponse(IConfigurationAndLogAccessor accessors, ConfigurationDictionary serverConfig, string storeId) {
            if (serverConfig == null)
                return;

            try {
                // only allow one save at a time
                using (new SingleGlobalInstance(String.Concat(storeId, CachedServerConfigFile).GetHashCode().ToString(), 500)) {
                    // retry loop
                    for (int retry = 0; retry < 2; retry++) {
                        using (var dir = new IsolatedStorageDirectory(storeId)) {
                            try {
                                dir.WriteFile(CachedServerConfigFile, serverConfig);
                                break;
                            } catch (Exception ex) {
                                // File is being used by another process or thread or the file does not exist.
                                accessors.Log.FormattedError(ex, "Unable to save server config to local storage: {0}", ex.Message);
                                Thread.Sleep(50);
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                accessors.Log.Error(ex, "An error occurred while saving client configuration");
            }

            // apply the config values from the server to the current client configuration
            foreach (string k in serverConfig.Keys)
                accessors.Configuration[k] = serverConfig[k];
        }

        internal static ClientConfiguration Create(ExceptionlessClient client) {
            var configuration = new ClientConfiguration();
            ClientConfigurationReader.Read(configuration, client);

            return configuration;
        }

        public const string TraceLogLimitKey = "TraceLogLimit";
        public const string SupportEmailKey = "SupportEmail";
    }
}