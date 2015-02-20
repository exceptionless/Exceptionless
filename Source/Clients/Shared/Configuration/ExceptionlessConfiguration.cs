using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Dependency;
using Exceptionless.Enrichments;
using Exceptionless.Enrichments.Default;
using Exceptionless.Models;

namespace Exceptionless {
    public class ExceptionlessConfiguration {
        private const string DEFAULT_SERVER_URL = "https://collector.exceptionless.io";
        private const string DEFAULT_USER_AGENT = "exceptionless/" + ThisAssembly.AssemblyFileVersion;
        private readonly IDependencyResolver _resolver;
        private bool _configLocked;
        private string _apiKey;
        private string _serverUrl;
        private ValidationResult _validationResult;
        private readonly List<string> _exclusions = new List<string>(); 

        public ExceptionlessConfiguration(IDependencyResolver resolver) {
            ServerUrl = DEFAULT_SERVER_URL;
            UserAgent = DEFAULT_USER_AGENT;
            Enabled = true;
            EnableSSL = true;
            DefaultTags = new TagSet();
            DefaultData = new DataDictionary();
            Settings = new SettingsDictionary();
            SubmissionBatchSize = 50;
            if (resolver == null)
                throw new ArgumentNullException("resolver");
            _resolver = resolver;

            EventEnrichmentManager.AddDefaultEnrichments(this);
        }

        internal bool IsLocked {
            get { return _configLocked; }
        }

        internal void LockConfig() {
            if (_configLocked)
                return;

            _configLocked = true;
            _validationResult = Validate();
            Enabled = Enabled && _validationResult.IsValid;
        }

        /// <summary>
        /// The server url that all events will be sent to.
        /// </summary>
        public string ServerUrl {
            get { return _serverUrl; }
            set {
                if (_configLocked && !_serverUrl.Equals(value))
                    throw new ArgumentException("ServerUrl can't be changed after the client has been initialized.");

                _serverUrl = value;
            }
        }

        /// <summary>
        /// Used to identify the client that sent the events to the server.
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// The API key that will be used when sending events to the server.
        /// </summary>
        public string ApiKey {
            get { return _apiKey; }
            set {
                if (_configLocked && !_apiKey.Equals(value))
                    throw new ArgumentException("ApiKey can't be changed after the client has been initialized.");

                _apiKey = value;
            }
        }

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
        /// Maximum number of events that should be sent to the server together in a batch. (Defaults to 50)
        /// </summary>
        public int SubmissionBatchSize { get; set; }

        /// <summary>
        /// A list of exclusion patterns that will automatically remove any data that matches them from any data submitted to the server.
        /// For example, entering CreditCard will remove any extended data properties, form fields, cookies and query
        /// parameters from the report.
        /// </summary>
        public IEnumerable<string> DataExclusions {
            get {
                if (Settings.ContainsKey(SettingsDictionary.KnownKeys.DataExclusions))
                    return _exclusions.Union(Settings.GetStringCollection(SettingsDictionary.KnownKeys.DataExclusions));
                
                return _exclusions.ToArray();
            }
        }

        /// <summary>
        /// Add items to the list of exclusion patterns that will automatically remove any data that matches them from any data submitted to the server.
        /// For example, entering CreditCard will remove any extended data properties, form fields, cookies and query
        /// parameters from the report.
        /// </summary>
        /// <param name="exclusions">The list of exclusion patterns to add.</param>
        public void AddDataExclusions(params string[] exclusions) {
            _exclusions.AddRange(exclusions);
        }

        /// <summary>
        /// Add items to the list of exclusion patterns that will automatically remove any data that matches them from any data submitted to the server.
        /// For example, entering CreditCard will remove any extended data properties, form fields, cookies and query
        /// parameters from the report.
        /// </summary>
        /// <param name="exclusions">The list of exclusion patterns to add.</param>
        public void AddDataExclusions(IEnumerable<string> exclusions) {
            _exclusions.AddRange(exclusions);
        }

        /// <summary>
        /// The dependency resolver to use for this configuration.
        /// </summary>
        public IDependencyResolver Resolver { get { return _resolver; } }

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
        /// Register an enrichment to be used in this configuration.
        /// </summary>
        /// <param name="key">The key used to identify the enrichment.</param>
        /// <param name="factory">A factory method to create the enrichment.</param>
        public void AddEnrichment(string key, Func<ExceptionlessConfiguration, IEventEnrichment> factory) {
            _enrichments[key] = new Lazy<IEventEnrichment>(() => factory(this));
        }

        /// <summary>
        /// Register an enrichment to be used in this configuration.
        /// </summary>
        /// <param name="enrichmentAction">The action used to enrich the events.</param>
        public void AddEnrichment(Action<EventEnrichmentContext, Event> enrichmentAction) {
            _enrichments[Guid.NewGuid().ToString()] = new Lazy<IEventEnrichment>(() => new ActionEnrichment(enrichmentAction));
        }

        /// <summary>
        /// Register an enrichment to be used in this configuration.
        /// </summary>
        /// <param name="enrichmentAction">The action used to enrich the events.</param>
        public void AddEnrichment(Action<Event> enrichmentAction) {
            _enrichments[Guid.NewGuid().ToString()] = new Lazy<IEventEnrichment>(() => new ActionEnrichment((context, ev) => enrichmentAction(ev)));
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

        public ValidationResult Validate() {
            if (_validationResult != null)
                return _validationResult;

            var result = new ValidationResult();

            string key = ApiKey != null ? ApiKey.Trim() : null;
            if (String.IsNullOrEmpty(key) || String.Equals(key, "API_KEY_HERE", StringComparison.OrdinalIgnoreCase))
                result.Messages.Add("ApiKey is not set.");

            if (key != null && (key.Length < 10 || key.Contains(" ")))
                result.Messages.Add(String.Format("ApiKey \"{0}\" is not valid.", key));

            if (String.IsNullOrEmpty(ServerUrl))
                result.Messages.Add("ServerUrl is not set.");

            return result;
        }

        public class ValidationResult {
            public ValidationResult() {
                Messages = new List<string>();
            }

            public bool IsValid { get { return Messages.Count == 0; } }
            public ICollection<string> Messages { get; private set; }
        }
    }
}
