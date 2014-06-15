#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using StatsdClient;

namespace Exceptionless.Core.AppStats {
    public class AppStatsClient : IAppStatsClient {
        private readonly IStatsd _client;

        public AppStatsClient(string serverName = "127.0.0.1", int port = 12000) {
            string prefix = "ex";
            if (Settings.Current.WebsiteMode != WebsiteMode.Production)
                prefix += "." + Settings.Current.WebsiteMode.ToString().ToLower();

            _client = new Statsd(serverName, port, prefix: prefix, connectionType: ConnectionType.Udp);
        }

        public void Counter(string statName, int value = 1) {
            _client.LogCount(statName, value);
        }

        public void Gauge(string statName, double value) {
            _client.LogGauge(statName, (int)value);
        }

        public void Timer(string statName, long milliseconds) {
            _client.LogTiming(statName, milliseconds);
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
    }
}