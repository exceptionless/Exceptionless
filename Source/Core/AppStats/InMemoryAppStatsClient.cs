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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Exceptionless.Core.AppStats {
    public class InMemoryAppStatsClient : IAppStatsClient {
        private readonly ConcurrentDictionary<string, long> _counters = new ConcurrentDictionary<string, long>();
        private readonly ConcurrentDictionary<string, double> _gauges = new ConcurrentDictionary<string, double>();
        private readonly ConcurrentDictionary<string, Stack<long>> _timings = new ConcurrentDictionary<string, Stack<long>>();
        private Timer _statsDisplayTimer;

        public InMemoryAppStatsClient() {
            _statsDisplayTimer = new Timer(OnDisplayStats, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        private void OnDisplayStats(object state) {
            foreach (var key in _counters.Keys)
                Debug.WriteLine("Counter: {0} Value: {1}", key, _counters[key]);
            
            foreach (var key in _gauges.Keys)
                Debug.WriteLine("Gauge: {0} Value: {1}", key, _gauges[key]);
            
            foreach (var key in _timings.Keys)
                Debug.WriteLine("Timing: {0} Avg Value: {1}", key, _timings[key].Average());
            
            Debug.WriteLine("-----");
        }

        public void Counter(string statName, int value = 1) {
            _counters.AddOrUpdate(statName, value, (key, current) => current + value);
        }

        public void Gauge(string statName, double value) {
            _gauges.AddOrUpdate(statName, value, (key, current) => value);
        }

        public void Timer(string statName, long milliseconds) {
            _timings.AddOrUpdate(statName, key => new Stack<long>(new[] { milliseconds }), (key, timings) => {
                timings.Push(milliseconds);

                // only keep the last 20 timings.
                if (timings.Count > 20)
                    timings.Pop();

                return timings;
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
            return _gauges.ContainsKey(statName) ? _counters[statName] : 0d;
        }
    }
}