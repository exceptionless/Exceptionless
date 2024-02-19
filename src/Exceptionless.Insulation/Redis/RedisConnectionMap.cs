using Exceptionless.Core.Utility;
using StackExchange.Redis;

namespace Exceptionless.Insulation.Redis;

public sealed class RedisConnectionMapping : IConnectionMapping
{
    private const string KeyPrefix = "Hub:";
    private readonly IConnectionMultiplexer _muxer;

    public RedisConnectionMapping(IConnectionMultiplexer muxer)
    {
        _muxer = muxer;
    }

    public Task AddAsync(string key, string connectionId)
    {
        if (key is null)
            return Task.CompletedTask;

        return Database.SetAddAsync(String.Concat(KeyPrefix, key), connectionId);
    }

    private IDatabase Database => _muxer.GetDatabase();

    public async Task<ICollection<string>> GetConnectionsAsync(string key)
    {
        if (key is null)
            return new List<string>();

        var values = await Database.SetMembersAsync(String.Concat(KeyPrefix, key));
        return values.Select(v => v.ToString()).ToList();
    }

    public async Task<int> GetConnectionCountAsync(string key)
    {
        if (key is null)
            return 0;

        return (int)await Database.SetLengthAsync(String.Concat(KeyPrefix, key));
    }

    public Task RemoveAsync(string key, string connectionId)
    {
        if (key is null)
            return Task.CompletedTask;

        return Database.SetRemoveAsync(String.Concat(KeyPrefix, key), connectionId);
    }
}
