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

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<OAuthClientMetadataDocument?> GetClientMetadataAsync(string clientId)
    {
        if (!TryCreateClientMetadataDocumentUri(clientId, out var uri))
            return null;

        string cacheKey = GetCacheKey(clientId);
        var cached = await cacheClient.GetAsync<OAuthClientMetadataDocument>(cacheKey);
        if (cached.HasValue)
            return cached.Value;

        try
        {
            using var cts = new CancellationTokenSource(options.ClientMetadataDocumentRequestTimeout);
            if (!await IsPublicHostAsync(uri, cts.Token))
                return null;

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            if (response.Content.Headers.ContentLength > options.ClientMetadataDocumentMaxBytes)
                return null;

            await using var responseStream = await response.Content.ReadAsStreamAsync(cts.Token);
            await using var metadataStream = await ReadLimitedAsync(responseStream, options.ClientMetadataDocumentMaxBytes, cts.Token);
            var metadata = await JsonSerializer.DeserializeAsync<OAuthClientMetadataDocument>(metadataStream, JsonSerializerOptions, cts.Token);
            if (metadata is null)
                return null;

            await cacheClient.SetAsync(cacheKey, metadata, options.ClientMetadataDocumentCacheLifetime);
            return metadata;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException ex)
        {
            LogUnableToFetchMetadata(ex, clientId);
            return null;
        }
        catch (JsonException ex)
        {
            LogUnableToFetchMetadata(ex, clientId);
            return null;
        }
        catch (IOException ex)
        {
            LogUnableToFetchMetadata(ex, clientId);
            return null;
        }
        catch (CryptographicException ex)
        {
            LogUnableToFetchMetadata(ex, clientId);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            LogUnableToFetchMetadata(ex, clientId);
            return null;
        }
    }

    private void LogUnableToFetchMetadata(Exception ex, string clientId)
    {
        logger.LogWarning(ex, "Unable to fetch OAuth client metadata document for {ClientId}.", clientId);
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

    private static bool IsPublicAddress(IPAddress address)
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
            && !(octets[0] == 192 && octets[1] == 168);
    }

    private static string GetCacheKey(string clientId)
    {
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(clientId))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return CachePrefix + hash;
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
