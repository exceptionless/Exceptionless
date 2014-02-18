using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Exceptionless.Models;

namespace Exceptionless {
    public class Configuration {
        private const string DEFAULT_SERVER_URL = "https://collector.exceptionless.com";

        public Configuration() {
            ServerUrl = DEFAULT_SERVER_URL;
            Enabled = true;
            SslEnabled = true;
            DefaultTags = new TagSet();
            DefaultExtendedData = new ExtendedDataDictionary();
            Settings = new Dictionary<string, string>();
            DataExclusions = new Collection<string>();
        }

        /// <summary>
        /// The server url that all reports will be sent to.
        /// </summary>
        public string ServerUrl { get; set; }

        /// <summary>
        /// The API key that will be used when sending reports to the server.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Whether the client is currently enabled or not. If it is disabled, submitted errors will be discarded and no data will be sent to the server.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Whether or not the client should use SSL when communicating with the server.
        /// </summary>
        public bool SslEnabled { get; set; }

        /// <summary>
        /// A default list of tags that will automatically be added to every report submitted to the server.
        /// </summary>
        public TagSet DefaultTags { get; private set; }

        /// <summary>
        /// A default list of of extended data objects that will automatically be added to every report submitted to the server.
        /// </summary>
        public ExtendedDataDictionary DefaultExtendedData { get; private set; }

        /// <summary>
        /// Contains a dictionary of custom settings that can be used to control the client and will be automatically updated from the server.
        /// </summary>
        public IDictionary<string, string> Settings { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include private information about the local machine.
        /// </summary>
        /// <value>
        /// <c>true</c> to include private information about the local machine; otherwise, <c>false</c>.
        /// </value>
        public bool IncludePrivateInformation { get; set; }

        /// <summary>
        /// A list of exclusion patterns that will automatically remove any data that matches them from any data submitted to the server.
        /// For example, entering CreditCard will remove any extended data properties, form fields, cookies and query
        /// parameters from the report.
        /// </summary>
        public ICollection<string> DataExclusions { get; set; }

        internal bool HasValidApiKey {
            get {
                string key = ApiKey != null ? ApiKey.Trim() : null;
                return !String.IsNullOrEmpty(key)
                       && key.Length >= 10
                       && !key.Contains(" ")
                       && !String.Equals(key, "API_KEY_HERE", StringComparison.OrdinalIgnoreCase);
            }
        }

        #region Default

        private static Configuration _defaultConfiguration = new Configuration();

        public static Configuration Default {
            get { return _defaultConfiguration; }
            set {
                if (value == null)
                    throw new ArgumentNullException("value");

                _defaultConfiguration = value;
            }
        }

        #endregion
    }
}
