using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Dependency;
using Exceptionless.Logging;
using Exceptionless.Models;
using TaskExtensions = Exceptionless.Extensions.TaskExtensions;

namespace Exceptionless.Queue {
    public class InMemoryEventQueue : IEventQueue, IDisposable {
        internal static readonly IEventQueue Instance = new InMemoryEventQueue();

        private readonly ConcurrentQueue<Event> _queue = new ConcurrentQueue<Event>();
        private Timer _queueTimer;
        private bool _processingQueue;

        public InMemoryEventQueue() {
            _queueTimer = new Timer(OnProcessQueue, null, TimeSpan.FromSeconds(5), TimeSpan.Zero);
        }

        public void Enqueue(Event ev) {
            _queue.Enqueue(ev);
        }

        public Task ProcessAsync() {
            Log.Info(typeof(InMemoryEventQueue), "Processing queue...");
            if (!Configuration.Enabled) {
                Log.Info(typeof(InMemoryEventQueue), "Configuration is disabled. The queue will not be processed.");
                // return canceled task.
            }

            _processingQueue = true;

            // TODO: Ensure they are under 256KB compressed.
            var events = new List<Event>();
            for (int i = 0; i < 5; i++) {
                Event ev;
                if (_queue.Count <= 0 || !_queue.TryDequeue(out ev))
                    break;

                events.Add(ev);
            }

            if (events.Count == 0) {
                _processingQueue = false;
                return TaskExtensions.FromResult(0);
            }

            var client = Configuration.Resolver.GetSubmissionClient();
            return client.SubmitAsync(events, Configuration).ContinueWith(t => {
                if (t.IsFaulted || t.IsCanceled || t.Exception != null || !t.Result.Success) {
                    Log.Info(typeof(InMemoryEventQueue), String.Concat("An error occurred while submitting the event. Exception: ", t.Exception.GetMessage()));
                    foreach (var ev in events)
                        Enqueue(ev);
                    
                    // TODO: Check the server if we should disable the queue..
                    SlowTimer();

                    _processingQueue = false;
                    return t;
                }

                //if(Configuration.CurrentConfigurationVersion < t.Result.SettingsVersion) {}
                t.ContinueWith(ts => client.GetSettingsAsync(Configuration).ContinueWith(r => {
                    if (r.IsFaulted || r.IsCanceled || r.Exception != null || !r.Result.Success) {
                        Log.Info(typeof(InMemoryEventQueue), String.Concat("An error occurred while getting the configuration settings. Exception: ", t.Exception.GetMessage()));
                    }

                    Configuration.Settings.Clear();
                    if(r.Result.Settings != null)
                        foreach (var setting in r.Result.Settings)
                            Configuration.Settings.Add(setting.Key, setting.Value);

                    _processingQueue = false;
                    // TODO: Fire event for Configuration Settings Updated.
                }));

                return t;
            });
        }

        public Configuration Configuration { get; set; }

        private IExceptionlessLog Log { get { return Configuration.Resolver.GetLog(); } }

        private void OnProcessQueue(object state)
        {
            if(!_processingQueue)
                ProcessAsync().Wait();
        }

        private void StopTimer()
        {
            Log.Info(typeof(InMemoryEventQueue), "Stopping timer.");
            _queueTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void QuickTimer(double milliseconds = 5000)
        {
            Log.Info(typeof(InMemoryEventQueue), "Triggering quick timer...");
            if (_processingQueue)
                return;

            _queueTimer.Change(TimeSpan.FromMilliseconds(milliseconds), TimeSpan.Zero);
            Log.Info(typeof(InMemoryEventQueue), "Quick timer scheduled");
        }

        private void SlowTimer()
        {
            Log.Info(typeof(InMemoryEventQueue), "Triggering slow timer...");
            if (_processingQueue)
                return;

            _queueTimer.Change(TimeSpan.FromHours(1), TimeSpan.Zero);
            Log.Info(typeof(InMemoryEventQueue), "Slow timer scheduled");
        }

        public void Dispose() {
            if (_queueTimer != null) {
                _queueTimer.Dispose();
                _queueTimer = null;
            }
        }
    }
}