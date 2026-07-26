using Exceptionless.Core.Services;
using Foundatio.Caching;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class OAuthDeviceServiceTests
{
    [Fact]
    public async Task RemoveUserCodeMappingIfOwnedAsync_RemovesOnlyCurrentOwner()
    {
        using var cacheClient = new InMemoryCacheClient();
        const string userCodeCacheKey = "oauth:user-code:test";
        const string staleDeviceCodeHash = "stale-device-code-hash";
        const string replacementDeviceCodeHash = "replacement-device-code-hash";
        await cacheClient.SetAsync(userCodeCacheKey, replacementDeviceCodeHash, TimeSpan.FromMinutes(1));

        await OAuthDeviceService.RemoveUserCodeMappingIfOwnedAsync(cacheClient, userCodeCacheKey, staleDeviceCodeHash);

        var reassignedUserCode = await cacheClient.GetAsync<string>(userCodeCacheKey);
        Assert.True(reassignedUserCode.HasValue);
        Assert.Equal(replacementDeviceCodeHash, reassignedUserCode.Value);

        await OAuthDeviceService.RemoveUserCodeMappingIfOwnedAsync(cacheClient, userCodeCacheKey, replacementDeviceCodeHash);

        Assert.False(await cacheClient.ExistsAsync(userCodeCacheKey));
    }
}
