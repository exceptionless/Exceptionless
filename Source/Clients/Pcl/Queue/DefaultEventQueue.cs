using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Dependency;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Utility;

namespace Exceptionless.Queue {
    public class DefaultEventQueue : IEventQueue, IDisposable {
        private readonly ConcurrentQueue<Event> _queue = new ConcurrentQueue<Event>();
        private Timer _queueTimer;
        private bool _processingQueue;

        public DefaultEventQueue() {
            // Wait 10 seconds to start processing to keep the startup resource demand low.
            _queueTimer = new Timer(OnProcessQueue, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));
        }

        public Task EnqueueAsync(Event ev) {
            _queue.Enqueue(ev);
            return TaskHelper.FromResult(0);
        }

        public Task ProcessAsync() {
            Log.Info(typeof(DefaultEventQueue), "Processing queue...");
            if (!Configuration.Enabled) {
                Log.Info(typeof(DefaultEventQueue), "Configuration is disabled. The queue will not be processed.");
                return TaskHelper.FromResult(0);
            }

            if (_queue.Count == 0) {
                Log.Info(typeof(DefaultEventQueue), "There are no events in the queue to process.");
                return TaskHelper.FromResult(0);
            }

            if (_processingQueue) {
                Log.Info(typeof(DefaultEventQueue), "The queue is already being processed.");
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

            var client = Configuration.Resolver.GetSubmissionClient();
            return client.SubmitAsync(events, Configuration).ContinueWith(t => {
                if (t.IsFaulted || t.IsCanceled || t.Exception != null || !t.Result.Success) {
                    Log.Info(typeof(DefaultEventQueue), String.Concat("An error occurred while submitting the event. Exception: ", t.Exception.GetMessage()));
                    foreach (var ev in events)
                        EnqueueAsync(ev);

                    SlowTimer();
                    _processingQueue = false;
                    return t;
                }

                if (Configuration.CurrentConfigurationVersion < t.Result.SettingsVersion) {
                    t.ContinueWith(ts => client.GetSettingsAsync(Configuration).ContinueWith(r => {
                        if (r.IsFaulted || r.IsCanceled || r.Exception != null || !r.Result.Success) {
                            Log.Info(typeof(DefaultEventQueue), String.Concat("An error occurred while getting the configuration settings. Exception: ", t.Exception.GetMessage()));
                        }

                        Configuration.Settings.Clear();
                        if (r.Result.Settings != null)
                            foreach (var setting in r.Result.Settings)
                                Configuration.Settings.Add(setting.Key, setting.Value);

                        _processingQueue = false;
                        // TODO: Fire event for Configuration Settings Updated.
                    }));
                }

                return t;
            });
        }

        public Configuration Configuration { get; set; }

        private IExceptionlessLog Log {
            get { return Configuration.Resolver.GetLog(); }
        }

        private void OnProcessQueue(object state) {
            if (!_processingQueue)
                ProcessAsync().Wait();
        }

        private void StopTimer() {
            Log.Info(typeof(DefaultEventQueue), "Stopping timer.");
            _queueTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void QuickTimer(double milliseconds = 5000) {
            Log.Info(typeof(DefaultEventQueue), "Triggering quick timer...");
            if (_processingQueue)
                return;

            _queueTimer.Change(TimeSpan.FromMilliseconds(milliseconds), TimeSpan.Zero);
            Log.Info(typeof(DefaultEventQueue), "Quick timer scheduled");
        }

        private void SlowTimer() {
            Log.Info(typeof(DefaultEventQueue), "Triggering slow timer...");
            if (_processingQueue)
                return;

            _queueTimer.Change(TimeSpan.FromHours(1), TimeSpan.Zero);
            Log.Info(typeof(DefaultEventQueue), "Slow timer scheduled");
        }

        public void Dispose() {
            if (_queueTimer != null) {
                _queueTimer.Dispose();
                _queueTimer = null;
            }
        }
    }
}