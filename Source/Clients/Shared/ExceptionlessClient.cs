#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Security;
using System.Text;
using System.Threading;
using Exceptionless.ExtendedData;
using Exceptionless.Extensions;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Net;
using Exceptionless.Plugins;
using Exceptionless.Queue;
using Exceptionless.Utility;
#if !PFX_LEGACY_3_5
using System.Threading.Tasks;
#endif
#if !SILVERLIGHT
using Exceptionless.Diagnostics;
#endif
using Config = Exceptionless.Configuration;

namespace Exceptionless {
    /// <summary>
    /// A class used to interact with the Exceptionless service.
    /// </summary>
    public class ExceptionlessClient : DisposableBase, IConfigurationAndLogAccessor {
        public const int ApiRevision = 1;

        private const string API_URI_FORMAT = "/api/v{0}/";

        private bool _updatingConfiguration;
        private readonly Timer _queueTimer;
        private const int QUEUE_INTERVAL_SECONDS = 10;
        private DateTime? _suspendProcessingUntil;
        private DateTime? _suspendErrorSubmissionUntil;
        private ILastErrorIdManager _lastErrorIdManager;
        private static volatile bool _processingQueue;
        private static readonly object _queueLock = new object();
        private readonly ObservableConcurrentDictionary<string, IExceptionlessPlugin> _plugins = new ObservableConcurrentDictionary<string, IExceptionlessPlugin>();
        private IExceptionlessLog _log;
        private static readonly IExceptionlessLog _nullLogger = new NullExceptionlessLog();

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
                Log.FormattedError(typeof(ExceptionlessClient), "Error updating configuration: {0}", e.Error.Message);
            else if (e.Configuration != null)
                Log.FormattedInfo(typeof(ExceptionlessClient), "Updated configuration to version {0}.", e.Configuration.Version);

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
                Log.FormattedError(typeof(ExceptionlessClient), "Sending error report failed: {0}", e.Error.Message);
            else
                Log.FormattedDebug(typeof(ExceptionlessClient), "Report completed for {0}.", e.ErrorId);

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

        /// <summary>
        /// Occurs when a request is about to be sent to the server. Can be used to customize the request object and proxy settings.
        /// </summary>
        public event EventHandler<RequestSendingEventArgs> RequestSending;

        /// <summary>
        /// Raises the <see cref="RequestSending" /> event.
        /// </summary>
        /// <param name="e">The <see cref="RequestSendingEventArgs" /> instance containing the event data.</param>
        protected void OnRequestSending(RequestSendingEventArgs e) {
            if (RequestSending != null)
                RequestSending(this, e);
        }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionlessClient" /> class.
        /// </summary>
        internal ExceptionlessClient(IQueueStore store = null, IExceptionlessLog log = null) {
            _log = log ?? _nullLogger;

            try {
                _configuration = Config.ClientConfiguration.Create(this);
                Log.FormattedTrace(typeof(ExceptionlessClient), "Configuration Values: ApiKey={0}, EnableSSL={1}, Enabled={2}, ServerUrl={3}", _configuration.ApiKey, _configuration.EnableSSL, _configuration.Enabled, _configuration.ServerUrl);
            } catch (Exception ex) {
                Log.FormattedError(typeof(ExceptionlessClient), "Critical error in ExceptionlessClient constructor: {0}", ex.Message);
            }

            try {
                _localConfiguration = Config.LocalConfigurationDictionary.Create(_configuration.StoreId, this);
            } catch (Exception ex) {
                Log.FormattedError(typeof(ExceptionlessClient), "Critical error in ExceptionlessClient constructor: {0}", ex.Message);
            }

            _queue = new QueueManager(this, store);
            _queueTimer = new Timer(OnQueueTimer, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(QUEUE_INTERVAL_SECONDS));
#if SILVERLIGHT
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
#else
            NetworkChange.NetworkAvailabilityChanged += NetworkChangeNetworkAvailabilityChanged;
#endif
        }

        private void OnQueueTimer(object state) {
            if (!Configuration.TestMode && !_processingQueue)
                ProcessQueue();
        }

        private void SendManifest(Manifest manifest) {
            try {
                SendReport(manifest);
            } catch (Exception ex) {
                manifest.LastError = ex.GetAllMessages();
                manifest.LogMessages.Add(String.Concat(DateTime.UtcNow.ToString(), " - ", ex.ToString()));
                Log.FormattedError(typeof(ExceptionlessClient), "Problem sending report {0}: {1}", manifest.Id, ex.Message);
            } finally {
                if (manifest.IsComplete() || manifest.ShouldDiscard()) {
                    try {
                        _queue.Delete(manifest.Id);
                    } catch (Exception) {
                        _queue.UpdateManifest(manifest);
                    }
                } else {
                    _queue.UpdateManifest(manifest);
                }
            }
        }

        private void SendReport(Manifest manifest) {
            Log.FormattedInfo(typeof(ExceptionlessClient), "Sending Manifest '{0}'", manifest.Id);

            if (manifest.IsSent || !manifest.ShouldRetry()) {
                Log.FormattedInfo(typeof(ExceptionlessClient), "Manifest was not submitted. IsSent: {0}, ShouldRetry: {1}, Attempts: {2}, Last Sent: {3}", manifest.IsSent, manifest.ShouldRetry(), manifest.Attempts, manifest.LastAttempt);
                return;
            }

            manifest.Attempts++;

            SendErrorCompletedEventArgs completed = null;
            Error error = _queue.GetError(manifest.Id);
            if (error == null) {
                manifest.LastError = String.Format("Could not load file '{0}'.", manifest.Id);
                manifest.IsSent = true;
                Log.FormattedInfo(typeof(ExceptionlessClient), manifest.LastError);
            } else
                manifest.IsSent = TrySendError(error, out completed);

            manifest.LastAttempt = DateTime.UtcNow;

            if (manifest.IsSent || completed == null)
                return;

            Log.FormattedError(typeof(ExceptionlessClient), completed.Error, "Problem processing manifest '{0}'.", manifest.Id);
            manifest.LogError(completed.Error);
            manifest.BreakProcessing = (completed.Error is SecurityException);
        }

        private bool TrySendError(Error error, out SendErrorCompletedEventArgs completed) {
            Exception exception = null;
            HttpWebResponse response = null;

            try {
                OnSendingError(error);
                Log.FormattedInfo(typeof(ExceptionlessClient), "Submiting error {0}...", error.Id);

                if (!Configuration.TestMode) {
                    RestClient client = CreateClient();
                    response = client.Post<Error, HttpWebResponse>("error", error);
                    exception = client.Error;
                }
            } catch (Exception ex) {
                exception = ex;
            }

            string id = null;

            if (response == null && !Configuration.TestMode)
                Log.FormattedError(typeof(ExceptionlessClient), "Error submit response was null: {0}", exception.Message);
            else {
                if (response.IsSuccessStatusCode() && !response.TryParseCreatedUri(out id))
                    exception = new Exception("Unable to parse the error id from the response object.", exception);

                if (response.ShouldUpdateConfiguration(LocalConfiguration.CurrentConfigurationVersion))
                    UpdateConfiguration(true);
            }

            completed = new SendErrorCompletedEventArgs(id, exception, false, error);
            OnSendErrorCompleted(completed);

            if (response != null) {
                // If there was a conflict, then the server already has the error and we should delete it locally
                if (response.StatusCode == HttpStatusCode.Conflict) {
                    Log.Info(typeof(ExceptionlessClient), "A duplicate error was submitted.");
                    return true;
                }

                // You are currently over your rate limit or the servers are under stress.
                if (response.StatusCode == HttpStatusCode.ServiceUnavailable) {
                    Log.Info(typeof(ExceptionlessClient), "Server returned service unavailable.");
                    SuspendProcessing();
                    return false;
                }

                // If the organization over the rate limit then discard the error.
                if (response.StatusCode == HttpStatusCode.PaymentRequired) {
                    Log.Info(typeof(ExceptionlessClient), "Too many errors have been submitted, please upgrade your plan.");
                    SuspendProcessing(suspendErrorSubmission: true, clearQueue: true);

                    return true;
                }

                // The api key was suspended or could not be authorized.
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden) {
                    Log.Info(typeof(ExceptionlessClient), "Unable to authenticate, please check your configuration. The error will not be submitted.");
                    SuspendProcessing(TimeSpan.FromMinutes(15));

                    return true;
                }

                // The service end point could not be found.
                if (response.StatusCode == HttpStatusCode.NotFound) {
                    Log.Info(typeof(ExceptionlessClient), "Unable to reach the service end point, please check your configuration. The error will not be submitted.");
                    SuspendProcessing(TimeSpan.FromHours(4));

                    return true;
                }
            }

            return response.IsSuccessStatusCode() || Configuration.TestMode;
        }

        private RestClient CreateClient() {
            Uri uri = GetServiceEndPoint();
            var client = new RestClient(uri) {
                UseMethodOverride = true,
                AuthorizationHeader = new AuthorizationHeader {
                    Scheme = ExceptionlessHeaders.Basic,
                    ParameterText = Convert.ToBase64String(Encoding.ASCII.GetBytes(String.Format("{0}:{1}", "client", Configuration.ApiKey)))
                },
                RequestSendingCallback = request => OnRequestSending(new RequestSendingEventArgs(request))
            };

            // TODO: auto detect blocked call at some point.

            return client;
        }

        private Uri GetServiceEndPoint() {
            var builder = new UriBuilder(_configuration.ServerUrl) {
                Path = String.Format(API_URI_FORMAT, ApiRevision)
            };

            // EnableSSL
            if (_configuration.EnableSSL && builder.Port == 80 && !builder.Host.Contains("local")) {
                builder.Port = 443;
                builder.Scheme = "https";
            }

            return builder.Uri;
        }

        /// <summary>
        /// Submits the error report.
        /// </summary>
        /// <param name="data">The error data.</param>
        public void SubmitError(Error data) {
            Log.FormattedInfo(typeof(ExceptionlessClient), "Submitting error: id={0} type={1}", data != null ? data.Id : "null", data != null ? data.Type : "null");
            if (data == null)
                throw new ArgumentNullException("data");

            if (!Configuration.Enabled) {
                Log.Info(typeof(ExceptionlessClient), "Configuration is disabled. The error will not be submitted.");
                return;
            }

            if (IsErrorSubmissionSuspended) {
                Log.Info(typeof(ExceptionlessClient), "Error submission is currently suspended. The error will not be submitted.");
                return;
            }

            if (CheckForDuplicateError(data))
                return;

            if (data.ExceptionlessClientInfo == null)
                data.ExceptionlessClientInfo = ExceptionlessClientInfoCollector.Collect(this, Configuration.IncludePrivateInformation);
            if (String.IsNullOrEmpty(data.Id))
                data.Id = ObjectId.GenerateNewId().ToString();
            _queue.Enqueue(data);

            Log.FormattedInfo(typeof(ExceptionlessClient), "Setting last error id '{0}'", data.Id);
            LastErrorIdManager.SetLast(data.Id);

            ProcessQueueAsync();
            SaveEmailAddress(data.UserEmail, false);
            LocalConfiguration.SubmitCount++;
            // TODO: This can be removed once we fix the bug in the ObservableConcurrentDictionary where IsDirty is not set immediately.
            LocalConfiguration.IsDirty = true;

            LocalConfiguration.Save();
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
            Log.FormattedInfo(typeof(ExceptionlessClient), "Processing unhandled exception of type '{0}'...", ex.GetType().FullName);

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
                Log.FormattedTrace(typeof(ExceptionlessClient), "Checking for duplicate exception: hash={0} type={1}", hashCode, current.Type);
                Log.FormattedTrace(typeof(ExceptionlessClient), "Error contents: {0}", current.ToString());

                // make sure that we don't process the same error multiple times within 2 seconds.
                if (_recentlyProcessedErrors.Any(s => s.Item1 == hashCode && s.Item2 >= repeatWindow)) {
                    Log.FormattedInfo(typeof(ExceptionlessClient), "Ignoring duplicate exception: type={0}", current.Type);
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

            return SubmitPatch(id, new {
                UserEmail = email,
                UserDescription = description
            });
        }

        /// <summary>
        /// Submits the patch for the specified error identifier.
        /// </summary>
        /// <param name="id">The error identifier of the error to patch.</param>
        /// <param name="patch">The patch document.</param>
        /// <returns></returns>
        public bool SubmitPatch(string id, object patch) {
            if (String.IsNullOrEmpty(id))
                throw new ArgumentNullException("id");
            if (patch == null)
                throw new ArgumentNullException("patch");

            HttpWebResponse response = null;
            Exception error;

            try {
                RestClient client = CreateClient();
                Log.FormattedInfo(typeof(ExceptionlessClient), "Submitting patch for {0}...", id);

                client.RequestHeaders[ExceptionlessHeaders.ConfigurationVersion] = LocalConfiguration.CurrentConfigurationVersion.ToString();
                response = client.Patch<object, HttpWebResponse>(String.Format("error/{0}", Uri.EscapeUriString(id)), patch);
                error = client.Error;
            } catch (Exception ex) {
                error = ex;
            }

            if (error != null)
                Log.FormattedError(typeof(ExceptionlessClient), "Submit patch for {0} failed: {1}.", id, error.Message);
            else if (response == null)
                Log.FormattedError(typeof(ExceptionlessClient), "Submit patch response was null {0}.", id);

            if (response != null && response.ShouldUpdateConfiguration(LocalConfiguration.CurrentConfigurationVersion))
                UpdateConfiguration(true);

            if (response.IsSuccessStatusCode())
                Log.FormattedInfo(typeof(ExceptionlessClient), "Submit patch completed for {0}.", id);

            return response.IsSuccessStatusCode();
        }

        /// <summary>
        /// Start processing the queue asynchronously.
        /// </summary>
        public void ProcessQueueAsync(double delay = 100) {
            _queueTimer.Change(TimeSpan.FromMilliseconds(delay), TimeSpan.FromSeconds(QUEUE_INTERVAL_SECONDS));
        }

        /// <summary>
        /// Process the queue.
        /// </summary>
        public void ProcessQueue() {
            if (!Configuration.Enabled) {
                Log.Info(typeof(ExceptionlessClient), "Configuration is disabled. The queue will not be processed.");
                return;
            }

            if (IsQueueProcessingSuspended)
                return;

            if (!HasNetworkConnection) {
                Log.Info(typeof(ExceptionlessClient), "No network connection is available, process the queue later.");
                SuspendProcessing();
                return;
            }

            if (_processingQueue)
                return;

            Log.Info(typeof(ExceptionlessClient), "Processing queue...");
            lock (_queueLock) {
                _processingQueue = true;

                try {
                    using (new SingleGlobalInstance(Configuration.ApiKey, 500)) {
                        if (IsQueueProcessingSuspended)
                            return;

                        try {
                            // discard older cases and make sure the queue isn't filling up
                            int count = _queue.Cleanup(DateTime.UtcNow.AddDays(-3));
                            if (count > 0)
                                Log.FormattedInfo(typeof(ExceptionlessClient), "Cleaning {0} old items from the queue.", count);

                            DateTime processReportsOlderThan = DateTime.Now;

                            // loop through reports getting 20 at a time until there are no more reports to be sent
                            List<Manifest> manifests = _queue.GetManifests(20, false, processReportsOlderThan).ToList();
                            while (manifests.Count > 0) {
                                Log.FormattedInfo(typeof(ExceptionlessClient), "Begin processing queue batch of {0} items...", manifests.Count);
                                foreach (Manifest manifest in manifests) {
                                    if (manifest.IsSent) {
                                        try {
                                            _queue.Delete(manifest.Id);
                                        } catch (Exception ex) {
                                            Log.Error(ex, "An error occurred while trying to delete a previously sent error.");
                                        }

                                        continue;
                                    }

                                    SendManifest(manifest);
                                    if (manifest.BreakProcessing || IsQueueProcessingSuspended)
                                        return;
                                }

                                manifests = _queue.GetManifests(20, false, processReportsOlderThan).ToList();
                            }
                        } catch (SecurityException se) {
                            SuspendProcessing();
                            Log.FormattedError(typeof(ExceptionlessClient), "Security exception while processing queue: {0}", se.Message);
                        } catch (Exception ex) {
                            Log.FormattedError(typeof(ExceptionlessClient), "Queue error: {0}", ex.Message);
                        }
                    }
                } catch (TimeoutException) {} catch (Exception ex) {
                    Log.FormattedError(typeof(ExceptionlessClient), ex, "Error trying to obtain instance lock: {0}", ex.Message);
                } finally {
                    _processingQueue = false;
                }
            }
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

            if (extendedData != null) {
                foreach (object o in extendedData)
                    error.AddObject(o);
            }

            if (tags != null)
                error.Tags.AddRange(tags);

            if (isCritical)
                error.MarkAsCritical();

            if (addDefaultInformation)
                error.AddDefaultInformation(contextData);

            return error;
        }

        internal static Error ToError(ExceptionlessClient client, Exception exception, string submissionMethod = "Manual", IDictionary<string, object> contextData = null) {
            Error error = exception.ToErrorModel();
            error.Id = ObjectId.GenerateNewId().ToString();
            error.OccurrenceDate = DateTimeOffset.Now;
            error.ExceptionlessClientInfo = ExceptionlessClientInfoCollector.Collect(client, client.Configuration.IncludePrivateInformation);
            error.ExceptionlessClientInfo.SubmissionMethod = submissionMethod;

            foreach (IExceptionlessPlugin plugin in client.Plugins) {
                try {
                    var ctx = new ExceptionlessPluginContext(client, contextData);
                    plugin.AfterCreated(ctx, error, exception);
                } catch (Exception ex) {
                    client.Log.FormattedError(typeof(ErrorExtensions), ex, "Error creating error model information: {0}", ex.Message);
                }
            }

            return error;
        }

        #region Last Error

        public ILastErrorIdManager LastErrorIdManager {
            get {
                if (_lastErrorIdManager == null)
                    _lastErrorIdManager = new DefaultLastErrorIdManager();

                return _lastErrorIdManager;
            }
            set { _lastErrorIdManager = value; }
        }

        /// <summary>
        /// Gets the last case message identifier that was submitted to the server.
        /// </summary>
        /// <returns>The message identifier</returns>
        public string GetLastErrorId() {
            return LastErrorIdManager.GetLast();
        }

        #endregion

        /// <summary>
        /// Disposes the managed resources.
        /// </summary>
        protected override void DisposeManagedResources() {
            if (_queueTimer != null)
                _queueTimer.Dispose();

            if (_localConfiguration != null)
                _localConfiguration.Save();
        }

        #region Properties

        private readonly QueueManager _queue;
        private readonly Config.ClientConfiguration _configuration;
        private readonly Config.LocalConfigurationDictionary _localConfiguration;

        private TagSet _tags;

        /// <summary>
        /// Configuration settings gathered from Exceptionless configuration attributes first, then the App/Web.config
        /// configuration section is applied, and lastly any values from the Exceptionless project configuration on the server.
        /// </summary>
        /// <value>The configurations settings.</value>
        public Config.ClientConfiguration Configuration { get { return _configuration; } }

        /// <summary>
        /// A dictionary of values that are persisted between runs of the current Exceptionless client installation.
        /// </summary>
        public Config.LocalConfigurationDictionary LocalConfiguration {
            get {
                // if _localConfiguration is null then return empty config dictionary just to be safe, it should not happen.
                return _localConfiguration ?? new Config.LocalConfigurationDictionary();
            }
        }

        public IExceptionlessLog Log {
            get { return _log ?? _nullLogger; }
            set {
                var currentLog = _log as IDisposable;
                if (currentLog != null)
                    currentLog.Dispose();

                // wrap logger to make sure it doesn't blow up the users app
                _log = new SafeExceptionlessLog(value);
            }
        }

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

        public TagSet Tags { get { return _tags ?? (_tags = new TagSet()); } }

        /// <summary>
        /// Gets the offline queue manager.
        /// </summary>
        /// <value>The offline queue manager.</value>
        internal QueueManager Queue { get { return _queue; } }

        #endregion

        public void SuspendProcessing(TimeSpan? suspensionTime = null, bool suspendErrorSubmission = false, bool clearQueue = false) {
            if (!suspensionTime.HasValue)
                suspensionTime = TimeSpan.FromMinutes(5);

            Log.Info(typeof(ExceptionlessClient), String.Format("Suspending processing for: {0}.", suspensionTime.Value));
            _suspendProcessingUntil = DateTime.Now.Add(suspensionTime.Value);
            _queueTimer.Change(suspensionTime.Value, TimeSpan.FromSeconds(QUEUE_INTERVAL_SECONDS));

            if (suspendErrorSubmission)
                _suspendErrorSubmissionUntil = DateTime.Now.Add(suspensionTime.Value);
            
            if (!clearQueue)
                return;

            // Account is over the limit and we want to ensure that the sample size being sent in will contain newer errors.
            try {
                _queue.Cleanup(DateTime.MaxValue);
            } catch (Exception) {}
        }

        private bool IsQueueProcessingSuspended {
            get { return _suspendProcessingUntil.HasValue && _suspendProcessingUntil.Value > DateTime.Now; }
        }

        private bool IsErrorSubmissionSuspended {
            get { return _suspendErrorSubmissionUntil.HasValue && _suspendErrorSubmissionUntil.Value > DateTime.Now; }
        }

        #region Singleton

        private static readonly Lazy<ExceptionlessClient> _manager = new Lazy<ExceptionlessClient>(
            () => new ExceptionlessClient());

        public static ExceptionlessClient Current { get { return _manager.Value; } }

        #endregion

        #region Configuration

        /// <summary>
        /// Updates the configuration.
        /// </summary>
        /// <param name="forceUpdate">if set to <c>true</c> to force update.</param>
        public void UpdateConfiguration(bool forceUpdate = false) {
            if (LocalConfiguration == null || _updatingConfiguration || (!forceUpdate && !IsConfigurationUpdateNeeded())) {
                Log.Info(typeof(ExceptionlessClient), "Configuration is up to date.");
                return;
            }

            Log.Info(typeof(ExceptionlessClient), "Updating configuration settings.");

            _updatingConfiguration = true;

            ClientConfiguration configuration = null;
            Exception error;

            try {
                RestClient client = CreateClient();
                configuration = client.Get<ClientConfiguration>("project/config");
                error = client.Error;
            } catch (Exception ex) {
                error = ex;
            }

            var args = new ConfigurationUpdatedEventArgs(configuration, error, false, _configuration);
            if (configuration == null) {
                Log.FormattedError(typeof(ExceptionlessClient), "Configuration response was null: {0}", error != null ? error.Message : "");
                LocalConfiguration.NextConfigurationUpdate = DateTime.UtcNow.AddHours(1);
            } else {
                Config.ClientConfiguration.ProcessServerConfigResponse(this, configuration.Settings, _configuration.StoreId);
                LocalConfiguration.CurrentConfigurationVersion = configuration.Version;
                LocalConfiguration.NextConfigurationUpdate = DateTime.UtcNow.AddDays(1);
            }

            // TODO: This can be removed once we fix the bug in the ObservableConcurrentDictionary where IsDirty is not set immediately.
            LocalConfiguration.IsDirty = true;

            LocalConfiguration.Save();

            _updatingConfiguration = false;

            OnConfigurationUpdated(args);
        }

        /// <summary>
        /// Updates the configuration Asynchronously.
        /// </summary>
        /// <param name="forceUpdate">if set to <c>true</c> to force update.</param>
        public void UpdateConfigurationAsync(bool forceUpdate = false) {
            if (!_updatingConfiguration)
                ThreadPool.QueueUserWorkItem(e => UpdateConfiguration(forceUpdate));
        }

        /// <summary>
        /// Determines whether configuration update is needed.
        /// </summary>
        /// <returns>
        /// <c>true</c> if a configuration update is needed; otherwise, <c>false</c>.
        /// </returns>
        public bool IsConfigurationUpdateNeeded() {
            // no network
            if (!HasNetworkConnection)
                return false;

            if (LocalConfiguration == null)
                return false;

            return LocalConfiguration.NextConfigurationUpdate <= DateTime.UtcNow;
        }

        private void SaveEmailAddress(string emailAddress, bool save) {
            if (String.Equals(LocalConfiguration.EmailAddress, emailAddress, StringComparison.OrdinalIgnoreCase))
                return;

            Log.FormattedInfo(typeof(ExceptionlessClient), "Saving email address '{0}'.", emailAddress);

            LocalConfiguration.EmailAddress = emailAddress;
            // TODO: This can be removed once we fix the bug in the ObservableConcurrentDictionary where IsDirty is not set immediately.
            LocalConfiguration.IsDirty = true;

            if (save)
                LocalConfiguration.Save();
        }

        #endregion

        #region Network

        private static bool? _hasNetworkConnection;

        private static bool HasNetworkConnection {
            get {
                if (!_hasNetworkConnection.HasValue)
                    UpdateNetworkConnectionStatus();

                return _hasNetworkConnection ?? true;
            }
        }

        private static void UpdateNetworkConnectionStatus() {
            try {
                _hasNetworkConnection = NetworkInterface.GetIsNetworkAvailable();
                if (!_hasNetworkConnection.Value)
                    _hasNetworkConnection = NetworkInterface.GetAllNetworkInterfaces().Any(x => x.OperationalStatus == OperationalStatus.Up);
            } catch (Exception ex) {
                Current.Log.FormattedError(typeof(ExceptionlessClient), "Unable to retrieve network status. Exception: {0}", ex.ToString());
                _hasNetworkConnection = true;
            }
        }

#if SILVERLIGHT
        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            UpdateNetworkConnectionStatus();
        }
#else
        private void NetworkChangeNetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e) {
            UpdateNetworkConnectionStatus();
        }
#endif

        #endregion

        private bool _startupCalled;
#if !SILVERLIGHT
        private ExceptionlessTraceListener _traceListener;
#endif

        public void Startup() {
            Startup(AppDomain.CurrentDomain);
        }

        public void Startup(AppDomain appDomain) {
            Log.Info(typeof(ExceptionlessClient), "Client startup.");

            try {
                appDomain.UnhandledException -= OnAppDomainUnhandledException;
                appDomain.UnhandledException += OnAppDomainUnhandledException;
#if !PFX_LEGACY_3_5
                TaskScheduler.UnobservedTaskException -= TaskSchedulerOnUnobservedTaskException;
                TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
#endif
            } catch (Exception ex) {
                Log.Error(typeof(ExceptionlessClient), ex, "An error occurred while wiring up to the unhandled exception events. This will happen when you are not running under full trust.");
            }

            if (!Configuration.HasValidApiKey)
                Log.Error(typeof(ExceptionlessClient), "Invalid Exceptionless API key. Please ensure that your API key is configured properly.");

            Log.Info(typeof(ExceptionlessClient), "Triggering configuration update and queue processing...");

            UpdateConfigurationAsync();
            ProcessQueueAsync(1000);

            Log.Info(typeof(ExceptionlessClient), "Done triggering configuration update and queue processing.");

#if !SILVERLIGHT
            if (Configuration.TraceLogLimit > 0) {
                if (_traceListener != null && Trace.Listeners.Contains(_traceListener))
                    Trace.Listeners.Remove(_traceListener);

                _traceListener = new ExceptionlessTraceListener(Configuration.TraceLogLimit);
                Trace.Listeners.Add(_traceListener);
            }
#endif

            if (!_startupCalled) {
                LocalConfiguration.StartCount++;
                // TODO: This can be removed once we fix the bug in the ObservableConcurrentDictionary where IsDirty is not set immediately.
                LocalConfiguration.IsDirty = true;
                LocalConfiguration.Save();
            }

            _startupCalled = true;

            Log.Info(typeof(ExceptionlessClient), "Startup done.");
        }

        public void Shutdown() {
            Shutdown(AppDomain.CurrentDomain);

            if (Log != null)
                Log.Flush();
        }

        public void Shutdown(AppDomain appDomain) {
            Log.Info(typeof(ExceptionlessClient), "Shutdown called.");

            try {
                appDomain.UnhandledException -= OnAppDomainUnhandledException;
#if !PFX_LEGACY_3_5
                TaskScheduler.UnobservedTaskException -= TaskSchedulerOnUnobservedTaskException;
#endif
            } catch (Exception ex) {
                Log.Error(typeof(ExceptionlessClient), ex, "An error occurred while unwiring the unhandled exception events. This will happen when you are not running under full trust.");
            }

#if !SILVERLIGHT
            if (_traceListener != null && Trace.Listeners.Contains(_traceListener))
                Trace.Listeners.Remove(_traceListener);
#endif

            if (Log != null)
                Log.Flush();
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e) {
            if (e.ExceptionObject is Exception)
                ProcessUnhandledException(e.ExceptionObject as Exception, "AppDomainUnhandledException");
        }

#if !PFX_LEGACY_3_5
        private void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs unobservedTaskExceptionEventArgs) {
            ProcessUnhandledException(unobservedTaskExceptionEventArgs.Exception, "UnobservedTaskException");
        }
#endif

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
            return ex.ToExceptionless();
        }
    }
}