using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Extensions;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Storage;
using Exceptionless.Submission;

namespace Exceptionless.Queue {
    public class DefaultEventQueue : IEventQueue {
        private readonly IExceptionlessLog _log;
        private readonly ExceptionlessConfiguration _config;
        private readonly ISubmissionClient _client;
        private readonly IFileStorage _storage;
        private readonly IJsonSerializer _serializer;
        private Timer _queueTimer;
        private bool _processingQueue;
        private readonly TimeSpan _processQueueInterval = TimeSpan.FromSeconds(10);
        private DateTime? _suspendProcessingUntil;
        private DateTime? _discardQueuedItemsUntil;

        public DefaultEventQueue(ExceptionlessConfiguration config, IExceptionlessLog log, ISubmissionClient client, IFileStorage fileStorage, IJsonSerializer serializer): this(config, log, client, fileStorage, serializer, null, null) {}

        public DefaultEventQueue(ExceptionlessConfiguration config, IExceptionlessLog log, ISubmissionClient client, IFileStorage fileStorage, IJsonSerializer serializer, TimeSpan? processQueueInterval, TimeSpan? queueStartDelay) {
            _log = log;
            _config = config;
            _client = client;
            _storage = fileStorage;
            _serializer = serializer;
            if (processQueueInterval.HasValue)
                _processQueueInterval = processQueueInterval.Value;

            _queueTimer = new Timer(OnProcessQueue, null, queueStartDelay ?? TimeSpan.FromSeconds(10), _processQueueInterval);
        }

        public void Enqueue(Event ev) {
            if (AreQueuedItemsDiscarded) {
                _log.Info(typeof(ExceptionlessClient), "Queue items are currently being discarded. The event will not be queued.");
                return;
            }

            _storage.Enqueue(_config.GetQueueName(), ev, _serializer);
        }

        public Task ProcessAsync() {
            return Task.Factory.StartNew(Process);
        }

        public void Process() {
            if (_processingQueue)
                return;

            _log.Info(typeof(DefaultEventQueue), "Processing queue...");
            if (!_config.Enabled) {
                _log.Info(typeof(DefaultEventQueue), "Configuration is disabled. The queue will not be processed.");
                return;
            }

            _processingQueue = true;
            
            try {
                _storage.CleanupQueueFiles(_config.GetQueueName());
                _storage.ReleaseStaleLocks(_config.GetQueueName());

                var batch = _storage.GetEventBatch(_config.GetQueueName(), _serializer);
                while (batch.Any()) {
                    bool deleteBatch = true;

                    try {
                        var response = _client.PostEvents(batch.Select(b => b.Item2), _config, _serializer);
                        if (response.ServiceUnavailable) {
                            // You are currently over your rate limit or the servers are under stress.
                            _log.Error(typeof(DefaultEventQueue), "Server returned service unavailable.");
                            SuspendProcessing();
                            deleteBatch = false;
                        } else if (response.PaymentRequired) {
                            // If the organization over the rate limit then discard the event.
                            _log.Info(typeof(DefaultEventQueue), "Too many events have been submitted, please upgrade your plan.");
                            SuspendProcessing(discardFutureQueuedItems: true, clearQueue: true);
                        } else if (response.UnableToAuthenticate) {
                            // The api key was suspended or could not be authorized.
                            _log.Info(typeof(DefaultEventQueue), "Unable to authenticate, please check your configuration. The event will not be submitted.");
                            SuspendProcessing(TimeSpan.FromMinutes(15));
                        } else if (response.NotFound || response.BadRequest) {
                            // The service end point could not be found.
                            _log.FormattedError(typeof(DefaultEventQueue), "Error while trying to submit data: {0}", response.Message);
                            SuspendProcessing(TimeSpan.FromHours(4));
                        } else if (!response.Success) {
                            _log.Error(typeof(DefaultEventQueue), String.Format("An error occurred while submitting events: {0}", response.Message));
                            SuspendProcessing();
                            deleteBatch = false;
                        }
                    } catch (Exception ex) {
                        _log.Error(typeof(DefaultEventQueue), ex, String.Concat("An error occurred while submitting events: ", ex.Message));
                        SuspendProcessing();
                        deleteBatch = false;
                    }

                    if (deleteBatch)
                        _storage.DeleteBatch(batch);
                    else
                        _storage.ReleaseBatch(batch);

                    if (!deleteBatch || IsQueueProcessingSuspended)
                        break;

                    batch = _storage.GetEventBatch(_config.GetQueueName(), _serializer);
                }
            } catch (Exception ex) {
                _log.Error(typeof(DefaultEventQueue), ex, String.Concat("An error occurred while processing the queue: ", ex.Message));
                SuspendProcessing();
            } finally {
                _processingQueue = false;
            }
        }

        private void OnProcessQueue(object state) {
            if (!IsQueueProcessingSuspended && !_processingQueue)
                Process();
        }

        public void SuspendProcessing(TimeSpan? duration = null, bool discardFutureQueuedItems = false, bool clearQueue = false) {
            if (!duration.HasValue)
                duration = TimeSpan.FromMinutes(5);

            _log.Info(typeof(ExceptionlessClient), String.Format("Suspending processing for: {0}.", duration.Value));
            _suspendProcessingUntil = DateTime.Now.Add(duration.Value);
            _queueTimer.Change(duration.Value, _processQueueInterval);

            if (discardFutureQueuedItems)
                _discardQueuedItemsUntil = DateTime.Now.Add(duration.Value);

            if (!clearQueue)
                return;

            // Account is over the limit and we want to ensure that the sample size being sent in will contain newer errors.
            try {
#pragma warning disable 4014
                _storage.CleanupQueueFiles(_config.GetQueueName(), TimeSpan.Zero);
#pragma warning restore 4014
            } catch (Exception) { }
        }

        private bool IsQueueProcessingSuspended {
            get { return _suspendProcessingUntil.HasValue && _suspendProcessingUntil.Value > DateTime.Now; }
        }

        private bool AreQueuedItemsDiscarded {
            get { return _discardQueuedItemsUntil.HasValue && _discardQueuedItemsUntil.Value > DateTime.Now; }
        }

        public void Dispose() {
            if (_queueTimer == null)
                return;

            _queueTimer.Dispose();
            _queueTimer = null;
        }
    }
}