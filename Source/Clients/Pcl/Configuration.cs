using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Exceptionless.Dependency;
using Exceptionless.Enrichments;
using Exceptionless.Models;

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

        public Configuration() : this(DependencyResolver.Default) {}

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
            get { return _resolver ?? DependencyResolver.Default; }
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

        #region Enrichments

        private readonly Dictionary<string, IEventEnrichment> _enrichments = new Dictionary<string, IEventEnrichment>();

        /// <summary>
        /// The list of plugins that will be used in this configuration.
        /// </summary>
        public IEnumerable<IEventEnrichment> Enrichments { get { return _enrichments.Values; } }

        /// <summary>
        /// Register an enrichment to be used in this configuration.
        /// </summary>
        /// <param name="enrichment">The enrichment to be used.</param>
        public void AddEnrichment(IEventEnrichment enrichment) {
            if (enrichment == null)
                return;

            AddEnrichment(enrichment.GetType().FullName, enrichment);
        }

        /// <summary>
        /// Register an enrichment to be used in this configuration.
        /// </summary>
        /// <param name="key">The key used to identify the enrichment.</param>
        /// <param name="enrichment">The enrichment to be used.</param>
        public void AddEnrichment(string key, IEventEnrichment enrichment) {
            if (_enrichments.ContainsKey(key))
                _enrichments[key] = enrichment;
            else
                _enrichments.Add(key, enrichment);
        }

        /// <summary>
        /// Remove an enrichment from this configuration.
        /// </summary>
        /// <param name="enrichment">The enrichment to be removed.</param>
        public void RemoveEnrichment(IEventEnrichment enrichment) {
            RemoveEnrichment(enrichment.GetType().FullName);
        }

        /// <summary>
        /// Remove an enrichment by key from this configuration.
        /// </summary>
        /// <param name="key">The key for the enrichment to be removed.</param>
        public void RemoveEnrichment(string key) {
            if (_enrichments.ContainsKey(key))
                _enrichments.Remove(key);
        }

        #endregion

        #region Default

        private static readonly Lazy<Configuration> _defaultConfiguration = new Lazy<Configuration>(() => new Configuration());

        public static Configuration Default {
            get { return _defaultConfiguration.Value; }
        }

        #endregion
    }
}
