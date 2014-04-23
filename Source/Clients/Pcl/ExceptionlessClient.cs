using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Dependency;
using Exceptionless.Duplicates;
using Exceptionless.Enrichments;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Queue;

namespace Exceptionless {
    public class ExceptionlessClient : IDisposable {
        private readonly Lazy<IExceptionlessLog> _log;
        private readonly Lazy<IEventQueue> _queue;
        private readonly Lazy<ILastReferenceIdManager> _lastReferenceIdManager;
        private readonly Lazy<IDuplicateChecker> _duplicateChecker;

        public ExceptionlessClient() : this(ExceptionlessConfiguration.CreateDefault()) { }

        public ExceptionlessClient(string apiKey) : this() {
            Configuration.ApiKey = apiKey;
        }

        public ExceptionlessClient(Action<ExceptionlessConfiguration> configure) : this(new ExceptionlessConfiguration(DependencyResolver.CreateDefault())) {
            if (configure != null)
                configure(Configuration);
        }

        public ExceptionlessClient(ExceptionlessConfiguration configuration) {
            if (configuration == null)
                throw new ArgumentNullException("configuration");

            Configuration = configuration;
            configuration.Resolver.Register(typeof(ExceptionlessConfiguration), () => Configuration);
            _log = new Lazy<IExceptionlessLog>(() => Configuration.Resolver.GetLog());
            _queue = new Lazy<IEventQueue>(() => Configuration.Resolver.GetEventQueue());
            _lastReferenceIdManager = new Lazy<ILastReferenceIdManager>(() => Configuration.Resolver.GetLastReferenceIdManager());
            _duplicateChecker = new Lazy<IDuplicateChecker>(() => Configuration.Resolver.GetDuplicateChecker());
        }

        public ExceptionlessConfiguration Configuration { get; private set; }

        /// <summary>
        /// Updates the user and description of an event for the specified reference id.
        /// </summary>
        /// <param name="referenceId">The reference id of the event to update.</param>
        /// <param name="email">The user's email address to set on the event.</param>
        /// <param name="description">The user's description of the event.</param>
        /// <returns></returns>
        public bool UpdateUserEmailAndDescription(string referenceId, string email, string description) {
            if (String.IsNullOrEmpty(referenceId))
                throw new ArgumentNullException("referenceId");
            if (String.IsNullOrEmpty(email) && String.IsNullOrEmpty(description))
                return true;

            return true;
            //return SubmitPatch(id, new {
            //    UserEmail = email,
            //    UserDescription = description
            //});
        }

        /// <summary>
        /// Start processing the queue asynchronously.
        /// </summary>
        public Task ProcessQueueAsync() {
            return _queue.Value.ProcessAsync();
        }

        /// <summary>
        /// Process the queue.
        /// </summary>
        public void ProcessQueue() {
            ProcessQueueAsync().Wait();
        }

        /// <summary>
        /// Submits the event to be sent to the server.
        /// </summary>
        /// <param name="ev">The event data.</param>
        /// <param name="enrichmentContextData">
        /// Any contextual data objects to be used by Exceptionless enrichments to gather default
        /// information for inclusion in the report information.
        /// </param>
        public void SubmitEvent(Event ev, IDictionary<string, object> enrichmentContextData = null) {
            if (!Configuration.Enabled) {
                _log.Value.Info(typeof(ExceptionlessClient), "Configuration is disabled. The error will not be submitted.");
                return;
            }

            if (ev == null)
                throw new ArgumentNullException("ev");

            var context = new EventEnrichmentContext(this, enrichmentContextData);
            EventEnrichmentManager.Enrich(context, ev);

            if (_duplicateChecker.Value.IsDuplicate(ev))
                return;

            // ensure all required data
            if (String.IsNullOrEmpty(ev.Type))
                ev.Type = Event.KnownTypes.Log;
            if (ev.Date == DateTimeOffset.MinValue)
                ev.Date = DateTimeOffset.Now;

            if (!OnSubmittingEvent(ev)) {
                _log.Value.FormattedInfo(typeof(ExceptionlessClient), "Event submission cancelled by event handler: id={0} type={1}", ev.ReferenceId, ev.Type);
                return;
            }

            _log.Value.FormattedInfo(typeof(ExceptionlessClient), "Submitting event: id={0} type={1}", ev.ReferenceId, ev.Type);
            _queue.Value.EnqueueAsync(ev).Wait();

            if (!String.IsNullOrEmpty(ev.ReferenceId)) {
                _log.Value.FormattedInfo(typeof(ExceptionlessClient), "Setting last reference id '{0}'", ev.ReferenceId);
                _lastReferenceIdManager.Value.SetLast(ev.ReferenceId);
            }

            //LocalConfiguration.SubmitCount++;
        }

        /// <summary>Creates a new instance of <see cref="Event" />.</summary>
        /// <param name="enrichmentContextData">
        /// Any contextual data objects to be used by Exceptionless enrichments to gather default
        /// information to add to the event data.
        /// </param>
        /// <returns>A new instance of <see cref="EventBuilder" />.</returns>
        public EventBuilder CreateEventBuilder(IDictionary<string, object> enrichmentContextData = null) {
            return new EventBuilder(new Event { Date = DateTimeOffset.Now }, this, enrichmentContextData);
        }

        /// <summary>
        /// Gets the last event client id that was submitted to the server.
        /// </summary>
        /// <returns>The event client id</returns>
        public string GetLastReferenceId() {
            return _lastReferenceIdManager.Value.GetLast();
        }

        #region Events

        /// <summary>
        /// Occurs when the configuration updated.
        /// </summary>
        public event EventHandler<ConfigurationUpdatedEventArgs> ConfigurationUpdated;

        /// <summary>
        /// Raises the <see cref="ConfigurationUpdated" /> event.
        /// </summary>
        /// <param name="e">The <see cref="ConfigurationUpdatedEventArgs" /> instance containing the event data.</param>
        protected void OnConfigurationUpdated(ConfigurationUpdatedEventArgs e) {
            if (e.Configuration != null)
                _log.Value.FormattedInfo(typeof(ExceptionlessClient), "Updated configuration to version {0}.", e.Configuration.Version);

            if (ConfigurationUpdated != null)
                ConfigurationUpdated(this, e);
        }

        /// <summary>
        /// Occurs when the event is being submitted.
        /// </summary>
        public event EventHandler<EventSubmittingEventArgs> SubmittingEvent;

        private bool OnSubmittingEvent(Event ev) {
            var args = new EventSubmittingEventArgs(ev);
            OnSubmittingEvent(args);
            return !args.Cancel;
        }

        /// <summary>
        /// Raises the <see cref="SubmittingEvent" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventSubmittingEventArgs" /> instance containing the event data.</param>
        protected void OnSubmittingEvent(EventSubmittingEventArgs e) {
            if (SubmittingEvent == null)
                return;

            var handlers = SubmittingEvent.GetInvocationList();
            foreach (var handler in handlers) {
                try {
                    handler.DynamicInvoke(this, e);
                    if (e.Cancel)
                        return;
                } catch (Exception ex) {
                    _log.Value.FormattedError(typeof(ExceptionlessClient), ex, "Error while invoking SubmittingEvent handler: {0}", ex.Message);
                }
            }
        }

        #endregion

        public void Dispose() {
            Configuration.Resolver.Dispose();
        }

        #region Default

        private static readonly Lazy<ExceptionlessClient> _defaultClient = new Lazy<ExceptionlessClient>(() => new ExceptionlessClient());

        [Obsolete("Please use ExceptionlessClient.Default instead.")]
        public static ExceptionlessClient Current {
            get { return _defaultClient.Value; }
        }

        public static ExceptionlessClient Default {
            get { return _defaultClient.Value; }
        }

        #endregion
    }
}
