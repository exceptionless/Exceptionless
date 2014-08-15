using System;
using System.Diagnostics;

namespace Exceptionless.Core.AppStats {
    public class AppStatsTimer : IDisposable {
        private readonly string _name;
        private readonly Stopwatch _stopWatch;
        private bool _disposed;
        private readonly IAppStatsClient _client;

        public AppStatsTimer(string name, IAppStatsClient client) {
            _name = name;
            _stopWatch = new Stopwatch();
            _client = client;
            _stopWatch.Start();
        }

        public void Dispose() {
            if (_disposed)
                return;

            _disposed = true;
            _stopWatch.Stop();
            _client.Timer(_name, _stopWatch.ElapsedMilliseconds);
        }
    }
}