using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Exceptionless.Api.Hubs {
    public class ConnectionMapping {
        private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new ConcurrentDictionary<string, HashSet<string>>();

        public void Add(string key, string connectionId) {
            if (key == null)
                return;

            _connections.AddOrUpdate(key, new HashSet<string>(new[] { connectionId }), (_, hs) => {
                hs.Add(connectionId);
                return hs;
            });
        }

        public ICollection<string> GetConnections(string key) {
            if (key == null)
                return new List<String>();

            return _connections.GetOrAdd(key, new HashSet<string>());
        }

        public void Remove(string key, string connectionId) {
            if (key == null)
                return;

            bool shouldRemove = false;
            _connections.AddOrUpdate(key, new HashSet<string>(), (_, hs) => {
                hs.Remove(connectionId);
                if (hs.Count == 0)
                    shouldRemove = true;

                return hs;
            });

            if (!shouldRemove)
                return;

            HashSet<string> connections;
            if (_connections.TryRemove(key, out connections) && connections.Count > 0)
                _connections.TryAdd(key, connections);
        }
    }
}
