using Foundatio.Caching;
using Microsoft.Extensions.Logging;
using UAParser;

namespace Exceptionless.Core.Utility;

public sealed class UserAgentParser
{
    private static readonly Lazy<Parser> _parser = new(() => Parser.GetDefault());
    private readonly InMemoryCacheClient _localCache;
    private readonly ILogger _logger;

    public UserAgentParser(TimeProvider timeProvider, ILoggerFactory loggerFactory)
    {
        _localCache = new InMemoryCacheClient(new InMemoryCacheClientOptions { MaxItems = 250, CloneValues = true, TimeProvider = timeProvider, LoggerFactory = loggerFactory });
        _logger = loggerFactory.CreateLogger<UserAgentParser>();
    }

    public async Task<ClientInfo?> ParseAsync(string userAgent)
    {
        if (String.IsNullOrEmpty(userAgent))
            return null;

        var cacheValue = await _localCache.GetAsync<ClientInfo>(userAgent);
        if (cacheValue.HasValue)
            return cacheValue.Value;

        ClientInfo? info = null;
        try
        {
            info = _parser.Value.Parse(userAgent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Unable to parse user agent {UserAgent}. Exception: {Message}", userAgent, ex.Message);
        }

        await _localCache.SetAsync(userAgent, info);
        return info;
    }
}
