#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using ServiceStack.Net30.Collections.Concurrent;

namespace Exceptionless.Core.AppStats {
    public class InMemoryAppStatsClient : IAppStatsClient {
        private readonly ConcurrentDictionary<string, long> _counters = new ConcurrentDictionary<string, long>();
        private readonly ConcurrentDictionary<string, double> _gauges = new ConcurrentDictionary<string, double>();

        public void Counter(string statName, int value = 1) {
            _counters.AddOrUpdate(statName, value, (key, current) => current + value);
        }

        public void Gauge(string statName, double value) {
            _gauges.AddOrUpdate(statName, value, (key, current) => value);
        }

        public void Timer(string statName, int milliseconds) {}

        public IDisposable StartTimer(string statName) {
            return new NullDisposable();
        }

        public void Time(Action action, string statName) {
            action();
        }

        public T Time<T>(Func<T> func, string statName) {
            return func();
        }

        public long GetCount(string statName) {
            return _counters.ContainsKey(statName) ? _counters[statName] : 0;
        }

        public double GetGaugeValue(string statName) {
            return _gauges.ContainsKey(statName) ? _counters[statName] : 0d;
        }

        private class NullDisposable : IDisposable {
            public void Dispose() {}
        }
    }
}