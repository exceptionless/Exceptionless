using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using StackExchange.Redis;

namespace Exceptionless.Insulation.Redis {
    public sealed class RedisConnectionMapping : IConnectionMapping {
        private const string KeyPrefix = "Hub:";
        private readonly ConnectionMultiplexer _muxer;

        public RedisConnectionMapping(ConnectionMultiplexer muxer) {
            _muxer = muxer;
        }

        public Task AddAsync(string key, string connectionId) {
            if (key == null)
                return Task.CompletedTask;

            return Database.SetAddAsync(String.Concat(KeyPrefix, key), connectionId);
        }

        private IDatabase Database => _muxer.GetDatabase();

        public async Task<ICollection<string>> GetConnectionsAsync(string key) {
            if (key == null)
                return new List<string>();

            var values = await Database.SetMembersAsync(String.Concat(KeyPrefix, key)).AnyContext();
            return values.Select(v => v.ToString()).ToList();
        }

        public async Task<int> GetConnectionCountAsync(string key) {
            if (key == null)
                return 0;

            return (int)await Database.SetLengthAsync(String.Concat(KeyPrefix, key)).AnyContext();
        }

        public Task RemoveAsync(string key, string connectionId) {
            if (key == null)
                return Task.CompletedTask;

            return Database.SetRemoveAsync(String.Concat(KeyPrefix, key), connectionId);
        }
    }
}
