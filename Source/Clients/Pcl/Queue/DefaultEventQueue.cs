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
    public class DefaultEventQueue : IEventQueue, IDisposable {
        private readonly IExceptionlessLog _log;
        private readonly ExceptionlessConfiguration _config;
        private readonly ISubmissionClient _client;
        private readonly IFileStorage _storage;
        private readonly IJsonSerializer _serializer;
        private Timer _queueTimer;
        private bool _processingQueue;

        public DefaultEventQueue(ExceptionlessConfiguration config, IExceptionlessLog log, ISubmissionClient client, IFileStorage fileStorage, IJsonSerializer serializer) {
            _log = log;
            _config = config;
            _client = client;
            _storage = fileStorage;
            _serializer = serializer;

            // Wait 10 seconds to start processing to keep the startup resource demand low.
            _queueTimer = new Timer(OnProcessQueue, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));
        }

        public Task EnqueueAsync(Event ev) {
            return _storage.SaveFileAsync(String.Concat("q\\", Guid.NewGuid().ToString("N"), ".0.json"), _serializer.Serialize(ev));
        }

        public async Task ProcessAsync() {
            _log.Info(typeof(DefaultEventQueue), "Processing queue...");
            if (!_config.Enabled) {
                _log.Info(typeof(DefaultEventQueue), "Configuration is disabled. The queue will not be processed.");
                return;
            }

            if (_processingQueue) {
                _log.Info(typeof(DefaultEventQueue), "The queue is already being processed.");
                return;
            }

            _processingQueue = true;

            var batch = await _storage.GetEventBatchAsync(_serializer);
            if (!batch.Any()) {
                _log.Info(typeof(DefaultEventQueue), "There are no events in the queue to process.");
                return;
            }

            var response = await _client.SubmitAsync(batch.Select(b => b.Item2), _config);
            if (response.Success) {
                await _storage.DeleteBatchAsync(batch);
            } else {
                _log.Info(typeof(DefaultEventQueue), String.Concat("An error occurred while submitting events: ", response.Message));
                await _storage.ReleaseBatchAsync(batch);
            }

            SlowTimer();
            _processingQueue = false;

            // TODO: Check to see if the configuration needs to be updated.
        }

        private void OnProcessQueue(object state) {
            if (!_processingQueue)
                ProcessAsync().Wait();
        }

        private void StopTimer() {
            _log.Info(typeof(DefaultEventQueue), "Stopping timer.");
            _queueTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void QuickTimer(double milliseconds = 5000) {
            _log.Info(typeof(DefaultEventQueue), "Triggering quick timer...");
            if (_processingQueue)
                return;

            _queueTimer.Change(TimeSpan.FromMilliseconds(milliseconds), TimeSpan.Zero);
            _log.Info(typeof(DefaultEventQueue), "Quick timer scheduled");
        }

        private void SlowTimer() {
            _log.Info(typeof(DefaultEventQueue), "Triggering slow timer...");
            if (_processingQueue)
                return;

            _queueTimer.Change(TimeSpan.FromHours(1), TimeSpan.Zero);
            _log.Info(typeof(DefaultEventQueue), "Slow timer scheduled");
        }

        public void Dispose() {
            if (_queueTimer == null)
                return;

            _queueTimer.Dispose();
            _queueTimer = null;
        }
    }
}