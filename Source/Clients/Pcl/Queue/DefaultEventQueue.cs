using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Submission;
using Exceptionless.Utility;

namespace Exceptionless.Queue {
    public class DefaultEventQueue : IEventQueue, IDisposable {
        private readonly ConcurrentQueue<Event> _queue = new ConcurrentQueue<Event>();
        private readonly IExceptionlessLog _log;
        private readonly ExceptionlessConfiguration _config;
        private readonly ISubmissionClient _client;
        private Timer _queueTimer;
        private bool _processingQueue;

        public DefaultEventQueue(ExceptionlessConfiguration config, IExceptionlessLog log, ISubmissionClient client) {
            _log = log;
            _config = config;
            _client = client;

            // Wait 10 seconds to start processing to keep the startup resource demand low.
            _queueTimer = new Timer(OnProcessQueue, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));
        }

        public Task EnqueueAsync(Event ev) {
            _queue.Enqueue(ev);
            return TaskHelper.FromResult(0);
        }

        public Task ProcessAsync() {
            _log.Info(typeof(DefaultEventQueue), "Processing queue...");
            if (!_config.Enabled) {
                _log.Info(typeof(DefaultEventQueue), "Configuration is disabled. The queue will not be processed.");
                return TaskHelper.FromResult(0);
            }

            if (_queue.Count == 0) {
                _log.Info(typeof(DefaultEventQueue), "There are no events in the queue to process.");
                return TaskHelper.FromResult(0);
            }

            if (_processingQueue) {
                _log.Info(typeof(DefaultEventQueue), "The queue is already being processed.");
                return TaskHelper.FromResult(0);
            }

            _processingQueue = true;

            // TODO: Make this size configurable.
            var events = new List<Event>();
            for (int i = 0; i < 20; i++) {
                Event ev;
                if (_queue.Count <= 0 || !_queue.TryDequeue(out ev))
                    break;

                events.Add(ev);
            }

            return _client.SubmitAsync(events, _config).ContinueWith(t => {
                if (t.IsFaulted || t.IsCanceled || t.Exception != null || !t.Result.Success) {
                    _log.Info(typeof(DefaultEventQueue), String.Concat("An error occurred while submitting the event: ", t.Result != null ? t.Result.Message : t.Exception.GetMessage()));
                    foreach (var ev in events)
                        EnqueueAsync(ev);

                    SlowTimer();
                    _processingQueue = false;
                    return t;
                }

                //if (Configuration.CurrentConfigurationVersion < t.Result.SettingsVersion) {
                //    t.ContinueWith(ts => client.GetSettingsAsync(Configuration).ContinueWith(r => {
                //        if (r.IsFaulted || r.IsCanceled || r.Exception != null || !r.Result.Success) {
                //            _log.Info(typeof(DefaultEventQueue), String.Concat("An error occurred while getting the configuration settings. Exception: ", t.Exception.GetMessage()));
                //        }

                //        Configuration.Settings.Clear();
                //        if (r.Result.Settings != null)
                //            foreach (var setting in r.Result.Settings)
                //                Configuration.Settings.Add(setting.Key, setting.Value);

                //        _processingQueue = false;
                //        // TODO: Fire event for Configuration Settings Updated.
                //    }));
                //}

                return t;
            });
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