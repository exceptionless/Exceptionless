using Exceptionless.Core.Services;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class OAuthServerOptions
{
    public TimeSpan AuthorizationCodeLifetime { get; internal set; } = TimeSpan.FromMinutes(5);
    public TimeSpan AccessTokenLifetime { get; internal set; } = TimeSpan.FromHours(1);
    public TimeSpan RefreshTokenLifetime { get; internal set; } = TimeSpan.FromDays(30);
    public TimeSpan DeviceCodeLifetime { get; internal set; } = TimeSpan.FromMinutes(15);
    public TimeSpan DeviceCodePollingInterval { get; internal set; } = TimeSpan.FromSeconds(5);
    public bool EnableClientIdMetadataDocuments { get; internal set; } = true;
    public int DynamicClientRegistrationIpLimit { get; internal set; } = 20;
    public int DeviceAuthorizationIpLimit { get; internal set; } = 120;
    public TimeSpan ClientMetadataDocumentCacheLifetime { get; internal set; } = TimeSpan.FromHours(1);
    public TimeSpan ClientMetadataDocumentRequestTimeout { get; internal set; } = TimeSpan.FromSeconds(5);
    public int ClientMetadataDocumentMaxBytes { get; internal set; } = 32 * 1024;

    public static OAuthServerOptions ReadFromConfiguration(IConfiguration config)
    {
        var options = new OAuthServerOptions();
        options.AuthorizationCodeLifetime = TimeSpan.FromMinutes(config.GetValue("OAuthServer:AuthorizationCodeLifetimeMinutes", 5));
        options.AccessTokenLifetime = TimeSpan.FromMinutes(config.GetValue("OAuthServer:AccessTokenLifetimeMinutes", 60));
        options.RefreshTokenLifetime = TimeSpan.FromDays(config.GetValue("OAuthServer:RefreshTokenLifetimeDays", 30));
        options.DeviceCodeLifetime = TimeSpan.FromMinutes(config.GetValue("OAuthServer:DeviceCodeLifetimeMinutes", 15));
        options.DeviceCodePollingInterval = TimeSpan.FromSeconds(config.GetValue("OAuthServer:DeviceCodePollingIntervalSeconds", 5));
        options.EnableClientIdMetadataDocuments = config.GetValue("OAuthServer:EnableClientIdMetadataDocuments", true);
        options.DynamicClientRegistrationIpLimit = config.GetValue("OAuthServer:DynamicClientRegistrationIpLimit", 20);
        options.DeviceAuthorizationIpLimit = config.GetValue("OAuthServer:DeviceAuthorizationIpLimit", 120);
        options.ClientMetadataDocumentCacheLifetime = TimeSpan.FromMinutes(config.GetValue("OAuthServer:ClientMetadataDocumentCacheLifetimeMinutes", 60));
        options.ClientMetadataDocumentRequestTimeout = TimeSpan.FromSeconds(config.GetValue("OAuthServer:ClientMetadataDocumentRequestTimeoutSeconds", 5));
        options.ClientMetadataDocumentMaxBytes = config.GetValue("OAuthServer:ClientMetadataDocumentMaxBytes", 32 * 1024);
        return options;
    }
}

public record OAuthClientOptions
{
    public required string ClientId { get; init; }
    public required string Name { get; init; }
    public IReadOnlyCollection<string> RedirectUris { get; init; } = [];
    public IReadOnlyCollection<string> Scopes { get; init; } = [];
    public IReadOnlyCollection<string> GrantTypes { get; init; } = [OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken];
    public bool IsDisabled { get; init; }

    internal OAuthClientOptions Normalize()
    {
        return this with
        {
            RedirectUris = RedirectUris
                .Where(u => !String.IsNullOrWhiteSpace(u))
                .Select(u => u.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Scopes = Scopes
                .Where(s => !String.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            GrantTypes = GrantTypes
                .Where(g => !String.IsNullOrWhiteSpace(g))
                .Select(g => g.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };
    }
}
