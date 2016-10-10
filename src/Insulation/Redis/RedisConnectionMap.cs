using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Utility;
using StackExchange.Redis;

namespace Exceptionless.Insulation.Redis {
    public class RedisConnectionMapping : IConnectionMapping {
        private const string KeyPrefix = "Hub:";
        private readonly ConnectionMultiplexer _muxer;

        public RedisConnectionMapping(ConnectionMultiplexer muxer) {
            _muxer = muxer;
        }

        public async Task AddAsync(string key, string connectionId) {
            if (key == null)
                return;

            await Database.SetAddAsync(String.Concat(KeyPrefix, key), connectionId);
        }

        private IDatabase Database => _muxer.GetDatabase();

        public async Task<ICollection<string>> GetConnectionsAsync(string key) {
            if (key == null)
                return new List<string>();

            var values = await Database.SetMembersAsync(String.Concat(KeyPrefix, key));
            return values.Select(v => v.ToString()).ToList();
        }

        public async Task RemoveAsync(string key, string connectionId) {
            if (key == null)
                return;

            await Database.SetRemoveAsync(String.Concat(KeyPrefix, key), connectionId);
        }
    }
}
