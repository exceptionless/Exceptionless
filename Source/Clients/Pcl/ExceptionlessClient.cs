using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Dependency;
using Exceptionless.Extensions;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Plugins;
using Exceptionless.Utility;

namespace Exceptionless {
    public class ExceptionlessClient : IDisposable {
        private readonly Dictionary<string, IExceptionlessPlugin> _plugins = new Dictionary<string, IExceptionlessPlugin>();
        private readonly IExceptionlessLog _log;

        public ExceptionlessClient(Configuration configuration, IDependencyResolver resolver) {
            if (configuration == null)
                throw new ArgumentNullException("configuration");
            if (resolver == null)
                throw new ArgumentNullException("resolver");

            Configuration = configuration;
            Resolver = resolver;
            _log = Resolver.Resolve<IExceptionlessLog>(NullExceptionlessLog.Instance);
        }

        public ExceptionlessClient(string apiKey) : this(new Configuration { ApiKey = apiKey }, DependencyResolver.Current) {}

        public ExceptionlessClient() : this(Configuration.Current, DependencyResolver.Current) { }

        public Configuration Configuration { get; private set; }

        public IDependencyResolver Resolver { get; private set; }

        /// <summary>
        /// Submits the error report.
        /// </summary>
        /// <param name="data">The error data.</param>
        public void SubmitError(Error data) {
            _log.FormattedInfo(typeof(ExceptionlessClient), "Submitting error: id={0} type={1}", data != null ? data.Id : "null", data != null ? data.Type : "null");

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
            //if (String.IsNullOrEmpty(data.Id))
            //    data.Id = ObjectId.GenerateNewId().ToString();
            //_queue.Enqueue(data);

            //_log.FormattedInfo(typeof(ExceptionlessClient), "Setting last error id '{0}'", data.Id);
            //LastErrorIdManager.SetLast(data.Id);

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
        /// <param name="submissionMethod">The method that was used to collect the error.</param>
        /// <param name="includeDefaultInformation">Whether to add the default information to the case or not</param>
        /// <param name="contextData">Any additional contextual data that should be used during creation of the error information.</param>
        public void ProcessUnhandledException(Exception ex, string submissionMethod, bool includeDefaultInformation = true, IDictionary<string, object> contextData = null) {
            Error error = CreateError(ex, addDefaultInformation: includeDefaultInformation, submissionMethod: submissionMethod, contextData: contextData);
            _log.FormattedInfo(typeof(ExceptionlessClient), "Processing unhandled exception of type '{0}'...", ex.GetType().FullName);

            var args = new UnhandledExceptionReportingEventArgs(ex, error);
            OnUnhandledExceptionReporting(args);
            if (args.Cancel)
                return;

            if (args.ShouldShowUI) {
                IExceptionlessPlugin uiPlugin = Plugins.FirstOrDefault(p => p.SupportsShowingUnhandledErrorSubmissionUI);
                if (uiPlugin != null) {
                    if (!uiPlugin.ShowUnhandledErrorSubmissionUI(new ExceptionlessPluginContext(this, contextData), error))
                        return;
                }
            }

            SubmitError(error);
        }

        private bool CheckForDuplicateError(ErrorInfo exception) {
            ErrorInfo current = exception;
            DateTime repeatWindow = DateTime.Now.AddSeconds(-2);

            while (current != null) {
                int hashCode = current.GetHashCode();
                _log.FormattedTrace(typeof(ExceptionlessClient), "Checking for duplicate exception: hash={0} type={1}", hashCode, current.Type);
                _log.FormattedTrace(typeof(ExceptionlessClient), "Error contents: {0}", current.ToString());

                // make sure that we don't process the same error multiple times within 2 seconds.
                if (_recentlyProcessedErrors.Any(s => s.Item1 == hashCode && s.Item2 >= repeatWindow)) {
                    _log.FormattedInfo(typeof(ExceptionlessClient), "Ignoring duplicate exception: type={0}", current.Type);
                    return true;
                }

                // add this exception to our list of recent errors that we have processed.
                _recentlyProcessedErrors.Enqueue(Tuple.Create(hashCode, DateTime.Now));

                // only keep the last 10 recent errors
                Tuple<int, DateTime> temp;
                while (_recentlyProcessedErrors.Count > 10)
                    _recentlyProcessedErrors.TryDequeue(out temp);

                current = current.Inner;
            }

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
        public void SubmitError(Exception ex, bool isCritical = false, bool addDefaultInformation = true, IEnumerable<string> tags = null, params object[] extendedData) {
            SubmitError(CreateError(ex, isCritical, addDefaultInformation, extendedData, tags));
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
        public void ProcessQueueAsync(double delay = 100) {
        }

        /// <summary>
        /// Process the queue.
        /// </summary>
        public void ProcessQueue() {
            _log.Info(typeof(ExceptionlessClient), "Processing queue...");
            if (!Configuration.Enabled) {
                _log.Info(typeof(ExceptionlessClient), "Configuration is disabled. The queue will not be processed.");
                //TODO: Should we call StopTimer here?
                return;
            }

            //if (_processingQueue)
            //    return;

            //lock (_queueLock) {
            //    _processingQueue = true;
            //    _isProcessQueueScheduled = false;
            //    bool useSlowTimer = false;
            //    StopTimer();

            //    try {
            //        using (new SingleGlobalInstance(Configuration.ApiKey, 500)) {
            //            try {
            //                // discard older cases and make sure the queue isn't filling up
            //                int count = _queue.Cleanup(DateTime.UtcNow.AddDays(-3));
            //                if (count > 0)
            //                    _log.FormattedInfo(typeof(ExceptionlessClient), "Cleaning {0} old items from the queue.", count);

            //                DateTime processReportsOlderThan = DateTime.Now;

            //                // loop through reports getting 20 at a time until there are no more reports to be sent
            //                List<Manifest> manifests = _queue.GetManifests(20, false, processReportsOlderThan).ToList();
            //                while (manifests.Count > 0) {
            //                    _log.FormattedInfo(typeof(ExceptionlessClient), "Begin processing queue batch of {0} items...", manifests.Count);
            //                    foreach (Manifest manifest in manifests) {
            //                        SendManifest(manifest);
            //                        if (!manifest.BreakProcessing)
            //                            continue;

            //                        useSlowTimer = true;
            //                        break;
            //                    }

            //                    manifests = _queue.GetManifests(20, false, processReportsOlderThan).ToList();
            //                }
            //            } catch (SecurityException se) {
            //                useSlowTimer = true;
            //                _log.FormattedError(typeof(ExceptionlessClient), "Security exception while processing queue: {0}", se.Message);
            //            } catch (Exception ex) {
            //                _log.FormattedError(typeof(ExceptionlessClient), "Queue error: {0}", ex.Message);
            //            }
            //        }
            //    } catch (TimeoutException) { } catch (Exception ex) {
            //        _log.FormattedError(typeof(ExceptionlessClient), ex, "Error trying to obtain instance lock: {0}", ex.Message);
            //    } finally {
            //        _processingQueue = false;

            //        if (useSlowTimer)
            //            SlowTimer();
            //        else
            //            PollTimer();
            //    }
            //}
        }

        /// <summary>Creates a new instance of <see cref="Error" />.</summary>
        /// <param name="ex">An <see cref="Exception" /> to initialize the <see cref="Error" /> with.</param>
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
        /// <param name="submissionMethod">The method that was used to collect the error.</param>
        /// <param name="contextData">Any additional contextual data that should be used during creation of the error information.</param>
        /// <returns>A new instance of <see cref="Error" />.</returns>
        public Error CreateError(Exception ex, bool isCritical = false, bool addDefaultInformation = true, IEnumerable<object> extendedData = null, IEnumerable<string> tags = null, string submissionMethod = "Manual", IDictionary<string, object> contextData = null) {
            Error error = ToError(this, ex, submissionMethod, contextData);

            //if (extendedData != null) {
            //    foreach (object o in extendedData)
            //        error.AddObject(o);
            //}

            //if (tags != null)
            //    error.Tags.AddRange(tags);

            //if (isCritical)
            //    error.MarkAsCritical();

            //if (addDefaultInformation)
            //    error.AddDefaultInformation(contextData);

            return error;
        }

        internal static Error ToError(ExceptionlessClient client, Exception exception, string submissionMethod = "Manual", IDictionary<string, object> contextData = null) {
            //Error error = exception.ToErrorModel();
            //error.Id = ObjectId.GenerateNewId().ToString();
            //error.OccurrenceDate = DateTimeOffset.Now;
            //error.ExceptionlessClientInfo = ExceptionlessClientInfoCollector.Collect(client, client.Configuration.IncludePrivateInformation);
            //error.ExceptionlessClientInfo.SubmissionMethod = submissionMethod;

            //foreach (IExceptionlessPlugin plugin in client.Plugins) {
            //    try {
            //        var ctx = new ExceptionlessPluginContext(client, contextData);
            //        plugin.AfterCreated(ctx, error, exception);
            //    } catch (Exception ex) {
            //        client._log.FormattedError(typeof(ErrorExtensions), ex, "Error creating error model information: {0}", ex.Message);
            //    }
            //}

            return new Error();
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
        public event EventHandler<ErrorModelEventArgs> SendingError;

        /// <summary>
        /// Occurs when the error has been sent to the server.
        /// </summary>
        public event EventHandler<SendErrorCompletedEventArgs> SendErrorCompleted;

        /// <summary>
        /// Raises the <see cref="SendErrorCompleted" /> event.
        /// </summary>
        /// <param name="e">The <see cref="SendErrorCompletedEventArgs" /> instance containing the event data.</param>
        protected void OnSendErrorCompleted(SendErrorCompletedEventArgs e) {
            if (e.Error != null)
                _log.FormattedError(typeof(ExceptionlessClient), "Sending error report failed: {0}", e.Error.Message);
            else
                _log.FormattedDebug(typeof(ExceptionlessClient), "Report completed for {0}.", e.ErrorId);

            if (SendErrorCompleted != null)
                SendErrorCompleted(this, e);
        }

        private void OnSendingError(Error error) {
            var args = new ErrorModelEventArgs(error);
            OnSendingError(args);
        }

        /// <summary>
        /// Raises the <see cref="SendingError" /> event.
        /// </summary>
        /// <param name="e">The <see cref="ErrorModelEventArgs" /> instance containing the event data.</param>
        protected void OnSendingError(ErrorModelEventArgs e) {
            if (SendingError != null)
                SendingError(this, e);
        }

        #endregion

        #region Plugins

        internal IEnumerable<IExceptionlessPlugin> Plugins { get { return _plugins.Values; } }

        public void RegisterPlugin(IExceptionlessPlugin plugin) {
            if (plugin == null)
                return;

            RegisterPlugin(plugin.GetType().FullName, plugin);
        }

        public void RegisterPlugin(string key, IExceptionlessPlugin plugin) {
            if (_plugins.ContainsKey(key))
                _plugins[key] = plugin;
            else
                _plugins.Add(key, plugin);
        }

        public void UnregisterPlugin(IExceptionlessPlugin plugin) {
            UnregisterPlugin(plugin.GetType().FullName);
        }

        public void UnregisterPlugin(string key) {
            if (_plugins.ContainsKey(key))
                _plugins.Remove(key);
        }

        #endregion

        public void Dispose() {
        }

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
            Current.SubmitError(ex, isCritical, addDefaultInformation, tags, extendedData);
        }

        /// <summary>
        /// Creates an error to be reported to the Exceptionless server.
        /// </summary>
        /// <param name="ex">The exception to submit.</param>
        public static ErrorBuilder Create(Exception ex) {
            return null; //ex.ToExceptionless();
        }

        #region Current

        private static ExceptionlessClient _currentClient = new ExceptionlessClient();

        public static ExceptionlessClient Current {
            get { return _currentClient; }
            set {
                if (value == null)
                    throw new ArgumentNullException("value");

                _currentClient = value;
            }
        }

        #endregion
    }
}
