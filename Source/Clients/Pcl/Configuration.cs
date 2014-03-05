using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Exceptionless.Dependency;
using Exceptionless.Models;
using Exceptionless.Plugins;

namespace Exceptionless {
    public class Configuration {
        private const string DEFAULT_SERVER_URL = "https://collector.exceptionless.com";
        private IDependencyResolver _resolver;

        public Configuration(IDependencyResolver resolver) {
            ServerUrl = DEFAULT_SERVER_URL;
            Enabled = true;
            SslEnabled = true;
            DefaultTags = new TagSet();
            DefaultData = new DataDictionary();
            Settings = new SettingsDictionary();
            DataExclusions = new Collection<string>();
            Resolver = resolver;
        }

        public Configuration() : this(DependencyResolver.Current) {}

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
        public DataDictionary DefaultData { get; private set; }

        /// <summary>
        /// Contains a dictionary of custom settings that can be used to control the client and will be automatically updated from the server.
        /// </summary>
        public SettingsDictionary Settings { get; private set; }

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

        /// <summary>
        /// The dependency resolver to use for this configuration.
        /// </summary>
        public IDependencyResolver Resolver {
            get { return _resolver ?? DependencyResolver.Current; }
            set {_resolver = value; }
        }

        internal bool HasValidApiKey {
            get {
                string key = ApiKey != null ? ApiKey.Trim() : null;
                return !String.IsNullOrEmpty(key)
                       && key.Length >= 10
                       && !key.Contains(" ")
                       && !String.Equals(key, "API_KEY_HERE", StringComparison.OrdinalIgnoreCase);
            }
        }

        public Configuration Clone() {
            return new Configuration(this.Resolver) {
                ServerUrl = this.ServerUrl,
                ApiKey = this.ApiKey,
                Enabled = this.Enabled,
                SslEnabled = this.SslEnabled,
                DefaultTags = new TagSet(this.DefaultTags.ToArray()),
                DefaultData = new DataDictionary(this.DefaultData.ToArray()),
                Settings = new SettingsDictionary(this.Settings.ToArray()),
                IncludePrivateInformation = this.IncludePrivateInformation,
                DataExclusions = new Collection<string>(this.DataExclusions.ToArray()),
                Resolver = this.Resolver
            };
        }

        #region Plugins

        private readonly Dictionary<string, IExceptionlessPlugin> _plugins = new Dictionary<string, IExceptionlessPlugin>();

        /// <summary>
        /// The list of plugins that will be used in this configuration.
        /// </summary>
        public IEnumerable<IExceptionlessPlugin> Plugins { get { return _plugins.Values; } }

        /// <summary>
        /// Register a plugin to be used in this configuration.
        /// </summary>
        /// <param name="plugin">The plugin to be used.</param>
        public void RegisterPlugin(IExceptionlessPlugin plugin) {
            if (plugin == null)
                return;

            RegisterPlugin(plugin.GetType().FullName, plugin);
        }

        /// <summary>
        /// Register a plugin to be used in this configuration.
        /// </summary>
        /// <param name="key">The key used to identify the plugin.</param>
        /// <param name="plugin">The plugin to be used.</param>
        public void RegisterPlugin(string key, IExceptionlessPlugin plugin) {
            if (_plugins.ContainsKey(key))
                _plugins[key] = plugin;
            else
                _plugins.Add(key, plugin);
        }

        /// <summary>
        /// Remove a plugin from this configuration.
        /// </summary>
        /// <param name="plugin">The plugin to be removed.</param>
        public void UnregisterPlugin(IExceptionlessPlugin plugin) {
            UnregisterPlugin(plugin.GetType().FullName);
        }

        /// <summary>
        /// Remove a plugin by key from this configuration.
        /// </summary>
        /// <param name="key">The key for the plugin to be removed.</param>
        public void UnregisterPlugin(string key) {
            if (_plugins.ContainsKey(key))
                _plugins.Remove(key);
        }

        #endregion

        #region Current

        private static Configuration _currentConfiguration = new Configuration();

        public static Configuration Current {
            get { return _currentConfiguration; }
            set {
                if (value == null)
                    throw new ArgumentNullException("value");

                _currentConfiguration = value;
            }
        }

        #endregion
    }
}
