using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Utility;

namespace Exceptionless.Api.Hubs {
    public class ConnectionMapping : IConnectionMapping {
        private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new ConcurrentDictionary<string, HashSet<string>>();

        public Task AddAsync(string key, string connectionId) {
            if (key == null)
                return Task.CompletedTask;

            _connections.AddOrUpdate(key, new HashSet<string>(new[] { connectionId }), (_, hs) => {
                hs.Add(connectionId);
                return hs;
            });

            return Task.CompletedTask;
        }

        public Task<ICollection<string>> GetConnectionsAsync(string key) {
            if (key == null)
                return Task.FromResult<ICollection<string>>(new List<string>());

            return Task.FromResult<ICollection<string>>(_connections.GetOrAdd(key, new HashSet<string>()));
        }

        public Task RemoveAsync(string key, string connectionId) {
            if (key == null)
                return Task.CompletedTask;

            bool shouldRemove = false;
            _connections.AddOrUpdate(key, new HashSet<string>(), (_, hs) => {
                hs.Remove(connectionId);
                if (hs.Count == 0)
                    shouldRemove = true;

                return hs;
            });

            if (!shouldRemove)
                return Task.CompletedTask;

            if (_connections.TryRemove(key, out HashSet<string> connections) && connections.Count > 0)
                _connections.TryAdd(key, connections);

            return Task.CompletedTask;
        }
    }
}
