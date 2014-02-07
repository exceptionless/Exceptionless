#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.Core.AppStats {
    public class NullAppStatsClient : IAppStatsClient {
        public void Counter(string statName, int value = 1) {}

        public void Gauge(string statName, double value) {}

        public void Timer(string statName, int milliseconds) {}

        public IDisposable StartTimer(string statName) {
            return new NullDisposable();
        }

        public void Time(Action action, string statName) {}

        public T Time<T>(Func<T> func, string statName) {
            return func();
        }

        private class NullDisposable : IDisposable {
            public void Dispose() {}
        }
    }
}