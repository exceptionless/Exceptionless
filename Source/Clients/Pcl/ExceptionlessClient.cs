using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Dependency;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Plugins;
using Exceptionless.Queue;

namespace Exceptionless {
    public class ExceptionlessClient : IDisposable {
        private readonly IExceptionlessLog _log;
        private readonly IEventQueue _queue;
        private readonly ILastClientIdManager _lastErrorIdManager;

        public ExceptionlessClient(Configuration configuration) {
            if (configuration == null)
                throw new ArgumentNullException("configuration");

            Configuration = configuration;
            _log = configuration.Resolver.GetLog();
            _queue = configuration.Resolver.GetEventQueue();
            _queue.Configuration = Configuration;
            _lastErrorIdManager = configuration.Resolver.GetLastErrorIdManager();

            if (_currentClient == null)
                _currentClient = this;
        }

        public ExceptionlessClient(string apiKey) : this(new Configuration { ApiKey = apiKey }) {}

        public ExceptionlessClient() : this(Configuration.Current) { }

        public Configuration Configuration { get; private set; }

        /// <summary>
        /// Submits the error report.
        /// </summary>
        /// <param name="data">The error data.</param>
        public void SubmitEvent(Event data) {
            _log.FormattedInfo(typeof(ExceptionlessClient), "Submitting event: id={0} type={1}", data != null ? data.ClientId : "null", data != null ? data.Type : "null");

            if (CheckForDuplicateError(data))
                return;

            if (!Configuration.Enabled) {
                _log.Info(typeof(ExceptionlessClient), "Configuration is disabled. The error will not be submitted.");
                return;
            }

            if (data == null)
                throw new ArgumentNullException("data");

            //if (data.ExceptionlessClientInfo == null)
            //    data.ExceptionlessClientInfo = ExceptionlessClientInfoCollector.Collect(this, Configuration.IncludePrivateInformation);
            if (String.IsNullOrEmpty(data.ClientId))
                data.ClientId = Guid.NewGuid().ToString("N");
            
            _queue.EnqueueAsync(data).Wait();

            _log.FormattedInfo(typeof(ExceptionlessClient), "Setting last event id '{0}'", data.ClientId);
            _lastErrorIdManager.SetLast(data.ClientId);

            //QuickTimer();
            //SaveEmailAddress(data.UserEmail, false);
            //LocalConfiguration.SubmitCount++;
            //// TODO: This can be removed once we fix the bug in the ObservableConcurrentDictionary where IsDirty is not set immediately.
            //LocalConfiguration.IsDirty = true;

            //LocalConfiguration.Save();
        }

        /// <summary>
        /// Process an unhandled exception.
        /// </summary>
        /// <param name="ex">The exception</param>
        /// <param name="includeDefaultInformation">Whether to add the default information to the case or not</param>
        /// <param name="contextData">Any additional contextual data that should be used during creation of the error information.</param>
        public void ProcessUnhandledException(Exception ex, bool includeDefaultInformation = true, IDictionary<string, object> contextData = null) {
            Event data = CreateEvent(ex, addDefaultInformation: includeDefaultInformation, contextData: contextData);
            _log.FormattedInfo(typeof(ExceptionlessClient), "Processing unhandled exception of type '{0}'...", ex.GetType().FullName);

            var args = new UnhandledExceptionReportingEventArgs(ex, data);
            OnUnhandledExceptionReporting(args);
            if (args.Cancel)
                return;

            if (args.ShouldShowUI) {
                IExceptionlessPlugin uiPlugin = Configuration.Plugins.FirstOrDefault(p => p.SupportsShowingUnhandledErrorSubmissionUI);
                if (uiPlugin != null) {
                    if (!uiPlugin.ShowUnhandledErrorSubmissionUI(new ExceptionlessPluginContext(this, contextData), data))
                        return;
                }
            }

            SubmitEvent(data);
        }

        private bool CheckForDuplicateError(Event data) {
            //ErrorInfo current = exception;
            //DateTime repeatWindow = DateTime.Now.AddSeconds(-2);

            //while (current != null) {
            //    int hashCode = current.GetHashCode();
            //    _log.FormattedTrace(typeof(ExceptionlessClient), "Checking for duplicate exception: hash={0} type={1}", hashCode, current.Type);
            //    _log.FormattedTrace(typeof(ExceptionlessClient), "Error contents: {0}", current.ToString());

            //    // make sure that we don't process the same error multiple times within 2 seconds.
            //    if (_recentlyProcessedErrors.Any(s => s.Item1 == hashCode && s.Item2 >= repeatWindow)) {
            //        _log.FormattedInfo(typeof(ExceptionlessClient), "Ignoring duplicate exception: type={0}", current.Type);
            //        return true;
            //    }

            //    // add this exception to our list of recent errors that we have processed.
            //    _recentlyProcessedErrors.Enqueue(Tuple.Create(hashCode, DateTime.Now));

            //    // only keep the last 10 recent errors
            //    Tuple<int, DateTime> temp;
            //    while (_recentlyProcessedErrors.Count > 10)
            //        _recentlyProcessedErrors.TryDequeue(out temp);

            //    current = current.Inner;
            //}

            return false;
        }

        private static readonly ConcurrentQueue<Tuple<int, DateTime>> _recentlyProcessedErrors = new ConcurrentQueue<Tuple<int, DateTime>>();

        /// <summary>
        /// Submits the exception.
        /// </summary>
        /// <param name="ex">The exception</param>
        /// <param name="isCritical">Mark this error occurrence as a critical error.</param>
        /// <param name="addDefaultInformation">
        /// Whether to add the default information like request info and machine info to the
        /// case or not.
        /// </param>
        /// <param name="tags">A list of tags to add to the error.</param>
        /// <param name="extendedData">
        /// As list of objects to add to the error's ExtendedData collection. If the object is an
        /// <see cref="ExtendedDataInfo">ExtendedDataInfo</see>, the settings from that will be used to add the ExtendedData.
        /// </param>
        public void SubmitEvent(Exception ex, bool isCritical = false, bool addDefaultInformation = true, IEnumerable<string> tags = null, params object[] extendedData) {
            SubmitEvent(CreateEvent(ex, isCritical, addDefaultInformation, extendedData, tags));
        }

        /// <summary>
        /// Updates the user and description of an error report for the specified error identifier.
        /// </summary>
        /// <param name="id">The error identifier of the error to patch.</param>
        /// <param name="email">The user's email address to set on the error.</param>
        /// <param name="description">The user's description of the error to set on the error.</param>
        /// <returns></returns>
        public bool UpdateUserEmailAndDescription(string id, string email, string description) {
            if (String.IsNullOrEmpty(id))
                throw new ArgumentNullException("id");
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
            return _queue.ProcessAsync();
        }

        /// <summary>
        /// Process the queue.
        /// </summary>
        public void ProcessQueue() {
            ProcessQueueAsync().Wait();
        }

        /// <summary>Creates a new instance of <see cref="Event" />.</summary>
        /// <param name="ex">An <see cref="Exception" /> to initialize the <see cref="Event" /> with.</param>
        /// <param name="isCritical">Mark this error occurrence as a critical error.</param>
        /// <param name="addDefaultInformation">
        /// Whether to add the default information like request info and machine info to the
        /// case or not.
        /// </param>
        /// <param name="extendedData">
        /// As list of objects to add to the error's ExtendedData collection. If the object is an
        /// <see cref="ExtendedDataInfo">ExtendedDataInfo</see>, the settings from that will be used to add the ExtendedData.
        /// </param>
        /// <param name="tags">A list of tags to add to the error.</param>
        /// <param name="contextData">Any additional contextual data that should be used during creation of the error information.</param>
        /// <returns>A new instance of <see cref="Event" />.</returns>
        public Event CreateEvent(Exception ex, bool isCritical = false, bool addDefaultInformation = true, IEnumerable<object> extendedData = null, IEnumerable<string> tags = null, IDictionary<string, object> contextData = null) {
            var builder = ex.ToExceptionless(addDefaultInformation, contextData, this);

            if (extendedData != null) {
                foreach (object o in extendedData)
                    builder.AddObject(o);
            }

            if (tags != null)
                builder.AddTags(tags.ToArray());

            if (isCritical)
                builder.MarkAsCritical();

            return builder.Target;
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
            if (e.Error != null)
                _log.FormattedError(typeof(ExceptionlessClient), "Error updating configuration: {0}", e.Error.Message);
            else if (e.Configuration != null)
                _log.FormattedInfo(typeof(ExceptionlessClient), "Updated configuration to version {0}.", e.Configuration.Version);

            if (ConfigurationUpdated != null)
                ConfigurationUpdated(this, e);
        }

        /// <summary>
        /// Occurs when an unhandled exception is about to be reported.
        /// </summary>
        public event EventHandler<UnhandledExceptionReportingEventArgs> UnhandledExceptionReporting;

        /// <summary>
        /// Raises the <see cref="UnhandledExceptionReporting" /> event.
        /// </summary>
        /// <param name="e">The <see cref="UnhandledExceptionReportingEventArgs" /> instance containing the event data.</param>
        protected void OnUnhandledExceptionReporting(UnhandledExceptionReportingEventArgs e) {
            if (UnhandledExceptionReporting != null)
                UnhandledExceptionReporting(this, e);
        }

        /// <summary>
        /// Occurs when the error is being sent to the server.
        /// </summary>
        public event EventHandler<EventModelEventArgs> SendingEvent;

        /// <summary>
        /// Occurs when the error has been sent to the server.
        /// </summary>
        public event EventHandler<SendEventCompletedEventArgs> SendEventCompleted;

        /// <summary>
        /// Raises the <see cref="SendEventCompleted" /> event.
        /// </summary>
        /// <param name="e">The <see cref="SendEventCompletedEventArgs" /> instance containing the event data.</param>
        protected void OnSendEventCompleted(SendEventCompletedEventArgs e) {
            if (e.Error != null)
                _log.FormattedError(typeof(ExceptionlessClient), "Sending event failed: {0}", e.Error.Message);
            else
                _log.FormattedDebug(typeof(ExceptionlessClient), "Report completed for {0}.", e.ErrorId);

            if (SendEventCompleted != null)
                SendEventCompleted(this, e);
        }

        private void OnSendingEvent(Event data) {
            OnSendingEvent(new EventModelEventArgs(data));
        }

        /// <summary>
        /// Raises the <see cref="SendingEvent" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventModelEventArgs" /> instance containing the event data.</param>
        protected void OnSendingEvent(EventModelEventArgs e) {
            if (SendingEvent != null)
                SendingEvent(this, e);
        }

        #endregion

        public void Dispose() { }

        /// <summary>
        /// Submit an error to be reported to the Exceptionless server.
        /// </summary>
        /// <param name="ex">The exception to submit.</param>
        /// <param name="isCritical">Mark this error occurrence as a critical error.</param>
        /// <param name="addDefaultInformation">
        /// Whether to add the default information like request info and machine info to the
        /// case or not.
        /// </param>
        /// <param name="tags">A list of tags to add to the error.</param>
        /// <param name="extendedData">
        /// As list of objects to add to the error's ExtendedData collection. If the object is an
        /// <see cref="ExtendedDataInfo">ExtendedDataInfo</see>, the settings from that will be used to add the ExtendedData.
        /// </param>
        public static void Submit(Exception ex, bool isCritical = false, bool addDefaultInformation = true, IEnumerable<string> tags = null, params object[] extendedData) {
            Current.SubmitEvent(ex, isCritical, addDefaultInformation, tags, extendedData);
        }

        /// <summary>
        /// Creates an error to be reported to the Exceptionless server.
        /// </summary>
        /// <param name="ex">The exception to submit.</param>
        public static EventBuilder Create(Exception ex) {
            return ex.ToExceptionless();
        }

        #region Current

        private static ExceptionlessClient _currentClient;

        public static ExceptionlessClient Current {
            get {
                if (_currentClient == null)
                    _currentClient = new ExceptionlessClient();
                
                return _currentClient;
            }
            set {
                if (value == null)
                    throw new ArgumentNullException("value");

                _currentClient = value;
            }
        }

        #endregion
    }
}
