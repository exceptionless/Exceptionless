using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptionless.Core.Configuration;
using Foundatio.Caching;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

public interface IOAuthClientMetadataService
{
    Task<OAuthClientMetadataDocument?> GetClientMetadataAsync(string clientId);
}

public sealed class OAuthClientMetadataService(HttpClient httpClient, OAuthServerOptions options, ICacheClient cacheClient, ILogger<OAuthClientMetadataService> logger) : IOAuthClientMetadataService
{
    private const string CachePrefix = "oauth:cimd:";
    private const string FailureCachePrefix = "oauth:cimd-failure:";
    private static readonly TimeSpan FailureCacheLifetime = TimeSpan.FromMinutes(5);

    public async Task<OAuthClientMetadataDocument?> GetClientMetadataAsync(string clientId)
    {
        if (!TryCreateClientMetadataDocumentUri(clientId, out var uri))
            return null;

        string cacheKey = GetCacheKey(clientId);
        string failureCacheKey = GetFailureCacheKey(clientId);
        var cachedFailure = await cacheClient.GetAsync<bool>(failureCacheKey);
        if (cachedFailure.HasValue)
            return null;

        var cached = await cacheClient.GetAsync<OAuthClientMetadataDocument>(cacheKey);
        if (cached.HasValue)
            return cached.Value;

        try
        {
            using var cts = new CancellationTokenSource(options.ClientMetadataDocumentRequestTimeout);
            if (!await IsPublicHostAsync(uri, cts.Token))
                return await CacheFailureAsync(failureCacheKey);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!response.IsSuccessStatusCode)
                return await CacheFailureAsync(failureCacheKey);

            if (response.Content.Headers.ContentLength > options.ClientMetadataDocumentMaxBytes)
                return await CacheFailureAsync(failureCacheKey);

            await using var responseStream = await response.Content.ReadAsStreamAsync(cts.Token);
            await using var metadataStream = await ReadLimitedAsync(responseStream, options.ClientMetadataDocumentMaxBytes, cts.Token);
            var metadata = await JsonSerializer.DeserializeAsync<OAuthClientMetadataDocument>(metadataStream, cancellationToken: cts.Token);
            if (metadata is null)
                return await CacheFailureAsync(failureCacheKey);

            await cacheClient.SetAsync(cacheKey, metadata, options.ClientMetadataDocumentCacheLifetime);
            return metadata;
        }
        catch (OperationCanceledException)
        {
            return await CacheFailureAsync(failureCacheKey);
        }
        catch (Exception ex) when (IsExpectedFetchError(ex))
        {
            LogUnableToFetchMetadata(ex, clientId);
            return await CacheFailureAsync(failureCacheKey);
        }
    }

    private void LogUnableToFetchMetadata(Exception ex, string clientId)
    {
        logger.LogWarning(ex, "Unable to fetch OAuth client metadata document for {ClientId}.", clientId);
    }

    private async Task<OAuthClientMetadataDocument?> CacheFailureAsync(string failureCacheKey)
    {
        await cacheClient.SetAsync(failureCacheKey, true, FailureCacheLifetime);
        return null;
    }

    private static bool IsExpectedFetchError(Exception ex)
    {
        return ex is HttpRequestException or JsonException or IOException or CryptographicException or InvalidOperationException;
    }

    public static bool TryCreateClientMetadataDocumentUri(string clientId, out Uri uri)
    {
        uri = null!;

        if (String.IsNullOrWhiteSpace(clientId) || !Uri.TryCreate(clientId.Trim(), UriKind.Absolute, out var parsedUri))
            return false;

        if (!String.Equals(parsedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!String.IsNullOrEmpty(parsedUri.Fragment) || !String.IsNullOrEmpty(parsedUri.UserInfo))
            return false;

        uri = parsedUri;
        return true;
    }

    private static async Task<MemoryStream> ReadLimitedAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var memoryStream = new MemoryStream();

        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memoryStream.Length + read > maxBytes)
                throw new InvalidOperationException("OAuth client metadata document exceeded the configured maximum size.");

            await memoryStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    private static async Task<bool> IsPublicHostAsync(Uri uri, CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.Host, out var address))
            addresses = [address];
        else
            addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);

        return addresses.Length > 0 && addresses.All(IsPublicAddress);
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address) || IPAddress.Any.Equals(address) || IPAddress.IPv6Any.Equals(address) || IPAddress.IPv6Loopback.Equals(address))
            return false;

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            byte[] bytes = address.GetAddressBytes();
            return !address.IsIPv6LinkLocal && !address.IsIPv6SiteLocal && (bytes[0] & 0xfe) != 0xfc;
        }

        byte[] octets = address.GetAddressBytes();
        return octets[0] != 0
            && octets[0] != 10
            && octets[0] != 127
            && !(octets[0] == 169 && octets[1] == 254)
            && !(octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31)
            && !(octets[0] == 100 && octets[1] >= 64 && octets[1] <= 127)
            && !(octets[0] == 192 && octets[1] == 168)
            && !(octets[0] == 198 && (octets[1] == 18 || octets[1] == 19));
    }

    private static string GetCacheKey(string clientId)
    {
        return CachePrefix + GetCacheKeyHash(clientId);
    }

    private static string GetFailureCacheKey(string clientId)
    {
        return FailureCachePrefix + GetCacheKeyHash(clientId);
    }

    private static string GetCacheKeyHash(string clientId)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(clientId))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

public sealed record OAuthClientMetadataDocument
{
    [JsonPropertyName("client_id")]
    public string? ClientId { get; init; }

    [JsonPropertyName("client_name")]
    public string? ClientName { get; init; }

    [JsonPropertyName("redirect_uris")]
    public string[]? RedirectUris { get; init; }

    [JsonPropertyName("grant_types")]
    public string[]? GrantTypes { get; init; }

    [JsonPropertyName("response_types")]
    public string[]? ResponseTypes { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; init; }
}
