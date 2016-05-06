using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Exceptionless.Core.Utility {
    public interface IConnectionMapping {
        Task AddAsync(string key, string connectionId);
        Task<ICollection<string>> GetConnectionsAsync(string key);
        Task RemoveAsync(string key, string connectionId);
    }

    public static class ConnectionMappingExtensions {
        public const string UserIdPrefix = "u-";
        public const string GroupPrefix = "g-";

        public static Task GroupAddAsync(this IConnectionMapping map, string group, string connectionId) {
            return map.AddAsync(GroupPrefix + group, connectionId);
        }

        public static Task GroupRemoveAsync(this IConnectionMapping map, string group, string connectionId) {
            return map.RemoveAsync(GroupPrefix + group, connectionId);
        }

        public static Task<ICollection<string>> GetGroupConnectionsAsync(this IConnectionMapping map, string group) {
            return map.GetConnectionsAsync(GroupPrefix + group);
        }

        public static Task UserIdAddAsync(this IConnectionMapping map, string userId, string connectionId) {
            return map.AddAsync(UserIdPrefix + userId, connectionId);
        }

        public static Task UserIdRemoveAsync(this IConnectionMapping map, string userId, string connectionId) {
            return map.RemoveAsync(UserIdPrefix + userId, connectionId);
        }

        public static Task<ICollection<string>> GetUserIdConnectionsAsync(this IConnectionMapping map, string userId) {
            return map.GetConnectionsAsync(UserIdPrefix + userId);
        }
    }
}
