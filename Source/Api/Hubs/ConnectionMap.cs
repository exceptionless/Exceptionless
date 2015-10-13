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

            _connections.AddOrUpdate(key, new HashSet<string>(), (_, hs) => {
                hs.Remove(connectionId);
                return hs;
            });
        }
    }
}
