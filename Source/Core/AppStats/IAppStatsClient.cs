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
    public interface IAppStatsClient {
        void Counter(string statName, int value = 1);

        void Gauge(string statName, double value);

        void Timer(string statName, long milliseconds);

        IDisposable StartTimer(string statName);

        void Time(Action action, string statName);

        T Time<T>(Func<T> func, string statName);
    }
}