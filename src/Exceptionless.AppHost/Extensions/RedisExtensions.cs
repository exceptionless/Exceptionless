using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Aspire.Hosting;

public static class RedisExtensions
{
    public static IResourceBuilder<RedisResource> WithClearCommand(
        this IResourceBuilder<RedisResource> builder)
    {
        builder.WithCommand(
            "clear-cache",
            "Clear Cache",
            async _ =>
            {
                var redisConnectionString = await builder.Resource.GetConnectionStringAsync() ??
                                            throw new InvalidOperationException("Unable to get the Redis connection string.");

                await using var connection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);

                await connection.GetDatabase().ExecuteAsync("FLUSHALL");

                return CommandResults.Success();
            },
            context => context.ResourceSnapshot.HealthStatus is HealthStatus.Healthy ? ResourceCommandState.Enabled : ResourceCommandState.Disabled);
        return builder;
    }
}
