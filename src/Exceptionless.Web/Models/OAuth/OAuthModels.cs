using System.Text.Json.Serialization;
using Exceptionless.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Models.OAuth;

public sealed record OAuthAuthorizeForm
{
    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }

    [JsonPropertyName("response_type")]
    public required string ResponseType { get; init; }

    [JsonPropertyName("redirect_uri")]
    public required string RedirectUri { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("code_challenge")]
    public required string CodeChallenge { get; init; }

    [JsonPropertyName("code_challenge_method")]
    public required string CodeChallengeMethod { get; init; }

    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    [JsonPropertyName("organization_ids")]
    public string[]? OrganizationIds { get; init; }

    public OAuthAuthorizeRequest ToRequest()
    {
        return new OAuthAuthorizeRequest
        {
            ClientId = ClientId,
            ResponseType = ResponseType,
            RedirectUri = RedirectUri,
            Scope = Scope,
            State = State,
            CodeChallenge = CodeChallenge,
            CodeChallengeMethod = CodeChallengeMethod,
            Resource = Resource,
            OrganizationIds = OrganizationIds ?? []
        };
    }
}

public sealed record OAuthAuthorizeResponse
{
    [JsonPropertyName("redirect_uri")]
    public required string RedirectUri { get; init; }
}

public sealed record OAuthAuthorizeConsentResponse
{
    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }

    [JsonPropertyName("client_name")]
    public required string ClientName { get; init; }

    [JsonPropertyName("redirect_uri")]
    public required string RedirectUri { get; init; }

    [JsonPropertyName("resource")]
    public required string Resource { get; init; }

    [JsonPropertyName("scopes")]
    public required IReadOnlyCollection<string> Scopes { get; init; }

    [JsonPropertyName("required_scopes")]
    public required IReadOnlyCollection<string> RequiredScopes { get; init; }
}

public sealed record OAuthAuthorizationServerMetadata
{
    [JsonPropertyName("issuer")]
    public required string Issuer { get; init; }

    [JsonPropertyName("authorization_endpoint")]
    public required string AuthorizationEndpoint { get; init; }

    [JsonPropertyName("token_endpoint")]
    public required string TokenEndpoint { get; init; }

    [JsonPropertyName("device_authorization_endpoint")]
    public required string DeviceAuthorizationEndpoint { get; init; }

    [JsonPropertyName("registration_endpoint")]
    public required string RegistrationEndpoint { get; init; }

    [JsonPropertyName("revocation_endpoint")]
    public required string RevocationEndpoint { get; init; }

    [JsonPropertyName("grant_types_supported")]
    public required IReadOnlyCollection<string> GrantTypesSupported { get; init; }

    [JsonPropertyName("response_types_supported")]
    public required IReadOnlyCollection<string> ResponseTypesSupported { get; init; }

    [JsonPropertyName("code_challenge_methods_supported")]
    public required IReadOnlyCollection<string> CodeChallengeMethodsSupported { get; init; }

    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public required IReadOnlyCollection<string> TokenEndpointAuthMethodsSupported { get; init; }

    [JsonPropertyName("scopes_supported")]
    public required IReadOnlyCollection<string> ScopesSupported { get; init; }

    [JsonPropertyName("resource_documentation")]
    public required string ResourceDocumentation { get; init; }

    [JsonPropertyName("client_id_metadata_document_supported")]
    public bool ClientIdMetadataDocumentSupported { get; init; }
}

public sealed record OAuthProtectedResourceMetadata
{
    [JsonPropertyName("resource")]
    public required string Resource { get; init; }

    [JsonPropertyName("authorization_servers")]
    public required IReadOnlyCollection<string> AuthorizationServers { get; init; }

    [JsonPropertyName("scopes_supported")]
    public required IReadOnlyCollection<string> ScopesSupported { get; init; }

    [JsonPropertyName("bearer_methods_supported")]
    public required IReadOnlyCollection<string> BearerMethodsSupported { get; init; }

    [JsonPropertyName("resource_documentation")]
    public required string ResourceDocumentation { get; init; }
}

public sealed record OAuthTokenForm
{
    [FromForm(Name = "grant_type")]
    public string GrantType { get; init; } = String.Empty;

    [FromForm(Name = "code")]
    public string? Code { get; init; }

    [FromForm(Name = "redirect_uri")]
    public string? RedirectUri { get; init; }

    [FromForm(Name = "client_id")]
    public string? ClientId { get; init; }

    [FromForm(Name = "code_verifier")]
    public string? CodeVerifier { get; init; }

    [FromForm(Name = "refresh_token")]
    public string? RefreshToken { get; init; }

    [FromForm(Name = "device_code")]
    public string? DeviceCode { get; init; }

    [FromForm(Name = "resource")]
    public string? Resource { get; init; }
}

public sealed record OAuthDeviceAuthorizationForm
{
    [FromForm(Name = "client_id")]
    public string? ClientId { get; init; }

    [FromForm(Name = "scope")]
    public string? Scope { get; init; }

    [FromForm(Name = "resource")]
    public string? Resource { get; init; }
}

public sealed record OAuthDeviceConsentForm
{
    [JsonPropertyName("user_code")]
    public string? UserCode { get; init; }
}

public sealed record OAuthDeviceAuthorizeForm
{
    [JsonPropertyName("user_code")]
    public string? UserCode { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("organization_ids")]
    public string[]? OrganizationIds { get; init; }
}

public sealed record OAuthDeviceConsentResponse
{
    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }

    [JsonPropertyName("client_name")]
    public required string ClientName { get; init; }

    [JsonPropertyName("user_code")]
    public required string UserCode { get; init; }

    [JsonPropertyName("resource")]
    public required string Resource { get; init; }

    [JsonPropertyName("scopes")]
    public required IReadOnlyCollection<string> Scopes { get; init; }

    [JsonPropertyName("required_scopes")]
    public required IReadOnlyCollection<string> RequiredScopes { get; init; }
}

public sealed record OAuthRevokeForm
{
    [FromForm(Name = "token")]
    public string? Token { get; init; }

    [FromForm(Name = "client_id")]
    public string? ClientId { get; init; }
}

public sealed record OAuthErrorResponse
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }
}
