using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Exceptionless.Dependency;
using Exceptionless.Enrichments;
using Exceptionless.Enrichments.Default;
using Exceptionless.Models;

namespace Exceptionless {
    public class ExceptionlessConfiguration {
        private const string DEFAULT_SERVER_URL = "https://collector.exceptionless.com";
        private const string DEFAULT_USER_AGENT = "exceptionless/" + ThisAssembly.AssemblyFileVersion;
        private readonly IDependencyResolver _resolver;

        public ExceptionlessConfiguration(IDependencyResolver resolver) {
            ServerUrl = DEFAULT_SERVER_URL;
            UserAgent = DEFAULT_USER_AGENT;
            Enabled = true;
            EnableSSL = true;
            DefaultTags = new TagSet();
            DefaultData = new DataDictionary();
            Settings = new SettingsDictionary();
            DataExclusions = new Collection<string>();
            if (resolver == null)
                throw new ArgumentNullException("resolver");
            _resolver = resolver;

            EventEnrichmentManager.AddDefaultEnrichments(this);
        }

        /// <summary>
        /// The server url that all events will be sent to.
        /// </summary>
        public string ServerUrl { get; set; }

        /// <summary>
        /// Used to identify the client that sent the events to the server.
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// The API key that will be used when sending events to the server.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Whether the client is currently enabled or not. If it is disabled, submitted errors will be discarded and no data will be sent to the server.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Whether or not the client should use SSL when communicating with the server.
        /// </summary>
        public bool EnableSSL { get; set; }

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
            get { return _resolver; }
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

        public void ApplyDefaultConfiguration() {
            foreach (var configurator in ConfigureDefaults)
                configurator(this);
        }

        static ExceptionlessConfiguration() {
            ConfigureDefaults = new List<Action<ExceptionlessConfiguration>>();
        }

        public static List<Action<ExceptionlessConfiguration>> ConfigureDefaults { get; private set; }

        public static ExceptionlessConfiguration CreateDefault() {
            var config = new ExceptionlessConfiguration(DependencyResolver.CreateDefault());
            config.ApplyDefaultConfiguration();
            return config;
        }

        #region Enrichments

        private readonly Dictionary<string, Lazy<IEventEnrichment>> _enrichments = new Dictionary<string, Lazy<IEventEnrichment>>();

        /// <summary>
        /// The list of plugins that will be used in this configuration.
        /// </summary>
        public IEnumerable<IEventEnrichment> Enrichments { get { return _enrichments.Values.Select(e => e.Value); } }

        /// <summary>
        /// Register an enrichment to be used in this configuration.
        /// </summary>
        /// <typeparam name="T">The enrichment type to be added.</typeparam>
        public void AddEnrichment<T>() where T : IEventEnrichment {
            AddEnrichment(typeof(T).FullName, typeof(T));
        }

        /// <summary>
        /// Register an enrichment to be used in this configuration.
        /// </summary>
        /// <param name="key">The key used to identify the enrichment.</param>
        /// <param name="enrichmentType">The enrichment type to be added.</param>
        public void AddEnrichment(string key, Type enrichmentType) {
            _enrichments[key] = new Lazy<IEventEnrichment>(() => Resolver.Resolve(enrichmentType) as IEventEnrichment);
        }

        /// <summary>
        /// Remove an enrichment from this configuration.
        /// </summary>
        /// <typeparam name="T">The enrichment type to be added.</typeparam>
        public void RemoveEnrichment<T>() where T : IEventEnrichment {
            RemoveEnrichment(typeof(T).FullName);
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
    }
}
