#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Exceptionless.Core.AppStats {
    public class InMemoryAppStatsClient : IAppStatsClient {
        private readonly ConcurrentDictionary<string, long> _counters = new ConcurrentDictionary<string, long>();
        private readonly ConcurrentDictionary<string, GaugeStats> _gauges = new ConcurrentDictionary<string, GaugeStats>();
        private readonly ConcurrentDictionary<string, TimingStats> _timings = new ConcurrentDictionary<string, TimingStats>();
        private readonly ConcurrentDictionary<string, AutoResetEvent> _counterEvents = new ConcurrentDictionary<string, AutoResetEvent>();
        private Timer _statsDisplayTimer;

        public InMemoryAppStatsClient() {
            _statsDisplayTimer = new Timer(OnDisplayStats, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        private void OnDisplayStats(object state) {
            DisplayStats();
        }

        public void DisplayStats() {
            foreach (var key in _counters.Keys.ToList())
                Debug.WriteLine("Counter: {0} Value: {1}", key, _counters[key]);

            foreach (var key in _gauges.Keys.ToList()) {
                Debug.WriteLine("Gauge: {0} Value: {1}", key, _gauges[key].Current.ToString("N0"));
                Debug.WriteLine("Gauge: {0} Avg Value: {1}", key, _gauges[key].Average.ToString("F"));
                Debug.WriteLine("Gauge: {0} Max Value: {1}", key, _gauges[key].Max.ToString("N0"));
            }

            foreach (var key in _timings.Keys.ToList()) {
                Debug.WriteLine("Timing: {0} Min Value: {1}ms", key, _timings[key].Min.ToString("N0"));
                Debug.WriteLine("Timing: {0} Avg Value: {1}ms", key, _timings[key].Average.ToString("F"));
                Debug.WriteLine("Timing: {0} Max Value: {1}ms", key, _timings[key].Max.ToString("N0"));
            }

            if (_counters.Count > 0 || _gauges.Count > 0 || _timings.Count > 0)
                Debug.WriteLine("-----");
        }

        public void Counter(string statName, int value = 1) {
            _counters.AddOrUpdate(statName, value, (key, current) => current + value);
            AutoResetEvent waitHandle;
            _counterEvents.TryGetValue(statName, out waitHandle);
            if (waitHandle != null)
                waitHandle.Set();
        }

        public bool WaitForCounter(string statName, long count = 1, double timeoutInSeconds = 10, Action work = null) {
            if (count == 0)
                return true;

            long currentCount = GetCount(statName);
            if (work != null)
                work();

            count = count - (GetCount(statName) - currentCount);

            if (count == 0)
                return true;

            var waitHandle = _counterEvents.GetOrAdd(statName, s => new AutoResetEvent(false));
            do {
                if (!waitHandle.WaitOne(TimeSpan.FromSeconds(timeoutInSeconds)))
                    return false;

                count--;
            } while (count > 0);

            return true;
        }

        public void Gauge(string statName, double value) {
            _gauges.AddOrUpdate(statName, key => new GaugeStats(value), (key, stats) => {
                stats.Set(value);
                return stats;
            });
        }

        public void Timer(string statName, long milliseconds) {
            _timings.AddOrUpdate(statName, key => new TimingStats(milliseconds), (key, stats) => {
                stats.Set(milliseconds);
                return stats;
            });
        }

        public IDisposable StartTimer(string statName) {
            return new AppStatsTimer(statName, this);
        }

        public void Time(Action action, string statName) {
            using (StartTimer(statName))
                action();
        }

        public T Time<T>(Func<T> func, string statName) {
            using (StartTimer(statName))
                return func();
        }

        public long GetCount(string statName) {
            return _counters.ContainsKey(statName) ? _counters[statName] : 0;
        }

        public double GetGaugeValue(string statName) {
            return _gauges.ContainsKey(statName) ? _gauges[statName].Current : 0d;
        }
    }

    public class TimingStats {
        public TimingStats(long value) {
            Set(value);
        }

        private long _current = 0;
        private long _max = 0;
        private long _min = 0;
        private int _count = 0;
        private long _total = 0;
        private double _average = 0d;

        public int Count { get { return _count; } }
        public long Total { get { return _total; } }
        public long Current { get { return _current; } }
        public long Min { get { return _min; } }
        public long Max { get { return _max; } }
        public double Average { get { return _average; } }

        private static readonly object _lock = new object();
        public void Set(long value) {
            lock (_lock) {
                _current = value;
                _count++;
                _total += value;
                _average = (double)_total / _count;

                if (value < _min || _min == 0)
                    _min = value;

                if (value > _max)
                    _max = value;
            }
        }
    }

    public class GaugeStats {
        public GaugeStats(double value) {
            Set(value);
        }

        private double _current = 0d;
        private double _max = 0d;
        private int _count = 0;
        private double _total = 0d;
        private double _average = 0d;

        public int Count { get { return _count; } }
        public double Total { get { return _total; } }
        public double Current { get { return _current; } }
        public double Max { get { return _max; } }
        public double Average { get { return _average; } }

        private static readonly object _lock = new object();
        public void Set(double value) {
            lock (_lock) {
                _current = value;
                _count++;
                _total += value;
                _average = _total / _count;

                if (value > _max)
                    _max = value;
            }
        }
    }
}