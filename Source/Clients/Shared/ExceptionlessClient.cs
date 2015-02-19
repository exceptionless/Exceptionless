using System;
using System.Threading.Tasks;
using Exceptionless.Dependency;
using Exceptionless.Duplicates;
using Exceptionless.Enrichments;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Exceptionless.Queue;
using Exceptionless.Submission;

namespace Exceptionless {
    public class ExceptionlessClient : IDisposable {
        private readonly Lazy<IExceptionlessLog> _log;
        private readonly Lazy<IEventQueue> _queue;
        private readonly Lazy<ISubmissionClient> _submissionClient;
        private readonly Lazy<ILastReferenceIdManager> _lastReferenceIdManager;
        private readonly Lazy<IDuplicateChecker> _duplicateChecker;

        public ExceptionlessClient() : this(new ExceptionlessConfiguration(DependencyResolver.CreateDefault())) { }

        public ExceptionlessClient(string apiKey) : this(new ExceptionlessConfiguration(DependencyResolver.CreateDefault())) {
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
            _queue = new Lazy<IEventQueue>(() => {
                // config can't be changed after the queue starts up.
                Configuration.LockConfig();
                return Configuration.Resolver.GetEventQueue();
            });

            _submissionClient = new Lazy<ISubmissionClient>(() => Configuration.Resolver.GetSubmissionClient());
            _lastReferenceIdManager = new Lazy<ILastReferenceIdManager>(() => Configuration.Resolver.GetLastReferenceIdManager());
            _duplicateChecker = new Lazy<IDuplicateChecker>(() => Configuration.Resolver.GetDuplicateChecker());
        }

        public ExceptionlessConfiguration Configuration { get; private set; }

        /// <summary>
        /// Updates the user's email address and description of an event for the specified reference id.
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

            if (!Configuration.Enabled) {
                _log.Value.Info(typeof(ExceptionlessClient), "Configuration is disabled. The event will not be updated with the user email and description.");
                return false;
            }

            if (!Configuration.IsLocked) {
                Configuration.LockConfig();
                if (!Configuration.Validate().IsValid) {
                    _log.Value.FormattedError(typeof(ExceptionlessClient), "Disabling client due to invalid configuration: {0}", String.Join(", ", Configuration.Validate().Messages));
                    return false;
                }
            }

            try {
                var response = _submissionClient.Value.PostUserDescription(referenceId, new UserDescription(email, description), Configuration, Configuration.Resolver.GetJsonSerializer());
                if (!response.Success)
                    _log.Value.FormattedError(typeof(ExceptionlessClient), "Failed to submit user email and description for event: {0} {1}", response.StatusCode, response.Message);

                return response.Success;
            } catch (Exception ex) {
                _log.Value.FormattedError(typeof(ExceptionlessClient), ex, "An error occurred while updating the user email and description for event: {0}.", referenceId);
                return false;
            }
        }

        /// <summary>
        /// Start processing the queue asynchronously.
        /// </summary>
        public Task ProcessQueueAsync() {
            if (!Configuration.Enabled) {
                _log.Value.Info(typeof(ExceptionlessClient), "Configuration is disabled. The queue will not be processed.");
                return Threading.Tasks.TaskExtensions.FromResult(0);
            }

            if (!Configuration.IsLocked) {
                Configuration.LockConfig();
                if (!Configuration.Validate().IsValid) {
                    _log.Value.FormattedError(typeof(ExceptionlessClient), "Disabling client due to invalid configuration: {0}", String.Join(", ", Configuration.Validate().Messages));
                    return Threading.Tasks.TaskExtensions.FromResult(0);
                }
            }

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
        public void SubmitEvent(Event ev, ContextData enrichmentContextData = null) {
            if (ev == null)
                throw new ArgumentNullException("ev");

            if (!Configuration.Enabled) {
                _log.Value.Info(typeof(ExceptionlessClient), "Configuration is disabled. The error will not be submitted.");
                return;
            }

            if (!Configuration.IsLocked) {
                Configuration.LockConfig();
                if (!Configuration.Validate().IsValid) {
                    _log.Value.FormattedError(typeof(ExceptionlessClient), "Disabling client due to invalid configuration: {0}", String.Join(", ", Configuration.Validate().Messages));
                    return;
                }
            }

            var context = new EventEnrichmentContext(this, enrichmentContextData);
            EventEnrichmentManager.Enrich(context, ev);

            if (_duplicateChecker.Value != null && _duplicateChecker.Value.IsDuplicate(ev))
                return;

            // ensure all required data
            if (String.IsNullOrEmpty(ev.Type))
                ev.Type = Event.KnownTypes.Log;
            if (ev.Date == DateTimeOffset.MinValue)
                ev.Date = DateTimeOffset.Now;

            if (!OnSubmittingEvent(ev, enrichmentContextData)) {
                _log.Value.FormattedInfo(typeof(ExceptionlessClient), "Event submission cancelled by event handler: id={0} type={1}", ev.ReferenceId, ev.Type);
                return;
            }

            _log.Value.FormattedTrace(typeof(ExceptionlessClient), "Submitting event: type={0}{1}", ev.Type, !String.IsNullOrEmpty(ev.ReferenceId) ? " refid=" + ev.ReferenceId : String.Empty);
            _queue.Value.Enqueue(ev);

            if (String.IsNullOrEmpty(ev.ReferenceId))
                return;

            _log.Value.FormattedTrace(typeof(ExceptionlessClient), "Setting last reference id '{0}'", ev.ReferenceId);
            _lastReferenceIdManager.Value.SetLast(ev.ReferenceId);
        }

        /// <summary>Creates a new instance of <see cref="Event" />.</summary>
        /// <param name="enrichmentContextData">
        /// Any contextual data objects to be used by Exceptionless enrichments to gather default
        /// information to add to the event data.
        /// </param>
        /// <returns>A new instance of <see cref="EventBuilder" />.</returns>
        public EventBuilder CreateEvent(ContextData enrichmentContextData = null) {
            return new EventBuilder(new Event { Date = DateTimeOffset.Now }, this, enrichmentContextData);
        }

        /// <summary>
        /// Gets the last event client id that was submitted to the server.
        /// </summary>
        /// <returns>The event client id</returns>
        public string GetLastReferenceId() {
            return _lastReferenceIdManager.Value.GetLast();
        }

        /// <summary>
        /// Occurs when the event is being submitted.
        /// </summary>
        public event EventHandler<EventSubmittingEventArgs> SubmittingEvent;

        private bool OnSubmittingEvent(Event ev, ContextData enrichmentContextData) {
            var args = new EventSubmittingEventArgs(this, ev, enrichmentContextData);
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

        void IDisposable.Dispose() {
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
