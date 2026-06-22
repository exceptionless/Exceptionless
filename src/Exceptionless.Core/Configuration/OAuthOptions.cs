using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class OAuthOptions
{
    public TimeSpan AuthorizationCodeLifetime { get; internal set; } = TimeSpan.FromMinutes(5);
    public TimeSpan AccessTokenLifetime { get; internal set; } = TimeSpan.FromHours(1);
    public TimeSpan RefreshTokenLifetime { get; internal set; } = TimeSpan.FromDays(30);
    public bool EnableClientIdMetadataDocuments { get; internal set; } = true;
    public TimeSpan ClientMetadataDocumentCacheLifetime { get; internal set; } = TimeSpan.FromHours(1);
    public TimeSpan ClientMetadataDocumentRequestTimeout { get; internal set; } = TimeSpan.FromSeconds(5);
    public int ClientMetadataDocumentMaxBytes { get; internal set; } = 32 * 1024;

    public static OAuthOptions ReadFromConfiguration(IConfiguration config)
    {
        var options = new OAuthOptions();
        options.AuthorizationCodeLifetime = TimeSpan.FromMinutes(config.GetValue("OAuth:AuthorizationCodeLifetimeMinutes", 5));
        options.AccessTokenLifetime = TimeSpan.FromMinutes(config.GetValue("OAuth:AccessTokenLifetimeMinutes", 60));
        options.RefreshTokenLifetime = TimeSpan.FromDays(config.GetValue("OAuth:RefreshTokenLifetimeDays", 30));
        options.EnableClientIdMetadataDocuments = config.GetValue("OAuth:EnableClientIdMetadataDocuments", true);
        options.ClientMetadataDocumentCacheLifetime = TimeSpan.FromMinutes(config.GetValue("OAuth:ClientMetadataDocumentCacheLifetimeMinutes", 60));
        options.ClientMetadataDocumentRequestTimeout = TimeSpan.FromSeconds(config.GetValue("OAuth:ClientMetadataDocumentRequestTimeoutSeconds", 5));
        options.ClientMetadataDocumentMaxBytes = config.GetValue("OAuth:ClientMetadataDocumentMaxBytes", 32 * 1024);
        return options;
    }
}

public record OAuthClientOptions
{
    public required string ClientId { get; init; }
    public required string Name { get; init; }
    public IReadOnlyCollection<string> RedirectUris { get; init; } = [];
    public IReadOnlyCollection<string> Scopes { get; init; } = [];
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
                .ToArray()
        };
    }
}
