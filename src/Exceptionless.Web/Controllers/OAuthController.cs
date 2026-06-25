using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Extensions;
using Foundatio.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace Exceptionless.Web.Controllers;

[Route("")]
public sealed class OAuthController(OAuthService oauthService, AppOptions appOptions, ICacheClient cacheClient, TimeProvider timeProvider) : ExceptionlessApiController(timeProvider)
{
    private const int DynamicClientRegistrationIpLimit = 20;

    [HttpGet(".well-known/oauth-authorization-server")]
    [AllowAnonymous]
    public ActionResult<OAuthAuthorizationServerMetadata> GetAuthorizationServerMetadataAsync()
    {
        string origin = GetOrigin();
        return Ok(new OAuthAuthorizationServerMetadata
        {
            Issuer = origin,
            AuthorizationEndpoint = $"{origin}/{API_PREFIX}/oauth/authorize",
            TokenEndpoint = $"{origin}/{API_PREFIX}/oauth/token",
            RegistrationEndpoint = $"{origin}/{API_PREFIX}/oauth/register",
            RevocationEndpoint = $"{origin}/{API_PREFIX}/oauth/revoke",
            GrantTypesSupported = [OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken],
            ResponseTypesSupported = ["code"],
            CodeChallengeMethodsSupported = [OAuthService.CodeChallengeMethod],
            TokenEndpointAuthMethodsSupported = ["none"],
            ScopesSupported = OAuthService.SupportedScopes,
            ResourceDocumentation = $"{origin}/mcp",
            ClientIdMetadataDocumentSupported = oauthService.ClientIdMetadataDocumentSupported
        });
    }

    [HttpGet(".well-known/oauth-protected-resource")]
    [AllowAnonymous]
    public ActionResult<OAuthProtectedResourceMetadata> GetProtectedResourceMetadataAsync()
    {
        string origin = GetOrigin();
        return Ok(new OAuthProtectedResourceMetadata
        {
            Resource = $"{origin}/mcp",
            AuthorizationServers = [origin],
            ScopesSupported = OAuthService.SupportedScopes,
            BearerMethodsSupported = ["header"],
            ResourceDocumentation = $"{origin}/mcp"
        });
    }

    [HttpGet(API_PREFIX + "/oauth/authorize")]
    [AllowAnonymous]
    public IActionResult AuthorizeAsync(
        [FromQuery(Name = "client_id")] string? clientId,
        [FromQuery(Name = "response_type")] string? responseType,
        [FromQuery(Name = "redirect_uri")] string? redirectUri,
        [FromQuery] string? scope,
        [FromQuery] string? state,
        [FromQuery(Name = "code_challenge")] string? codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod,
        [FromQuery] string? resource)
    {
        return RedirectToAuthorizeBridge();
    }

    [HttpPost(API_PREFIX + "/oauth/authorize")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public Task<IActionResult> CompleteAuthorizeAsync([FromBody] OAuthAuthorizeForm form)
    {
        return CompleteAuthorizationAsync(form.ToRequest(), jsonResponse: true);
    }

    [HttpPost(API_PREFIX + "/oauth/register")]
    [AllowAnonymous]
    public async Task<ActionResult<OAuthClientRegistrationResponse>> RegisterAsync([FromBody] OAuthClientRegistrationRequest request)
    {
        if (await IsDynamicClientRegistrationRateLimitedAsync())
            return StatusCode(StatusCodes.Status429TooManyRequests, new OAuthErrorResponse
            {
                Error = "temporarily_unavailable",
                ErrorDescription = "Too many dynamic client registration attempts."
            });

        var result = await oauthService.RegisterClientAsync(request);
        if (!result.IsSuccess)
            return OAuthError(result.Error, result.ErrorDescription);

        return StatusCode(StatusCodes.Status201Created, result.Response);
    }

    [HttpPost(API_PREFIX + "/oauth/token")]
    [AllowAnonymous]
    public async Task<ActionResult<OAuthTokenResponse>> TokenAsync([FromForm] OAuthTokenForm form)
    {
        var request = new OAuthTokenRequest
        {
            GrantType = form.GrantType,
            Code = form.Code,
            RedirectUri = form.RedirectUri,
            ClientId = form.ClientId,
            CodeVerifier = form.CodeVerifier,
            RefreshToken = form.RefreshToken,
            Resource = NormalizeMcpResource(form.Resource)
        };

        OAuthTokenIssueResult result = String.Equals(form.GrantType, OAuthGrantTypes.RefreshToken, StringComparison.Ordinal)
            ? await oauthService.RefreshAsync(request)
            : await oauthService.ExchangeAuthorizationCodeAsync(request);

        if (!result.IsSuccess)
            return OAuthError(result.Error, result.ErrorDescription);

        return Ok(result.Response);
    }

    [HttpPost(API_PREFIX + "/oauth/revoke")]
    [AllowAnonymous]
    public async Task<IActionResult> RevokeAsync([FromForm] OAuthRevokeForm form)
    {
        await oauthService.RevokeAsync(form.Token);
        return Ok();
    }

    private async Task<bool> IsDynamicClientRegistrationRateLimitedAsync()
    {
        string cacheKey = $"ip:{Request.GetClientIpAddress()}:oauth-dcr:attempts";
        long attempts = await cacheClient.IncrementAsync(cacheKey, 1, _timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromHours(1)));
        return attempts > DynamicClientRegistrationIpLimit;
    }

    private string GetOrigin()
    {
        return new Uri(appOptions.BaseURL).GetLeftPart(UriPartial.Authority);
    }

    private string GetMcpResource()
    {
        return $"{GetOrigin()}/mcp";
    }

    private string NormalizeMcpResource(string? resource)
    {
        return String.IsNullOrWhiteSpace(resource) ? GetMcpResource() : resource;
    }

    private ObjectResult OAuthError(string? error, string? description)
    {
        return BadRequest(new OAuthErrorResponse
        {
            Error = error ?? "invalid_request",
            ErrorDescription = description
        });
    }

    private async Task<IActionResult> CompleteAuthorizationAsync(OAuthAuthorizeRequest request, bool jsonResponse)
    {
        request = request with { Resource = NormalizeMcpResource(request.Resource) };
        var validation = await oauthService.ValidateAuthorizationRequestAsync(request, GetMcpResource());
        if (!validation.IsValid)
            return OAuthError(validation.Error, validation.ErrorDescription);

        string code = await oauthService.CreateAuthorizationCodeAsync(request, CurrentUser.Id);
        string redirectUri = BuildRedirectUri(request.RedirectUri, code, request.State);
        return jsonResponse ? Ok(new OAuthAuthorizeResponse { RedirectUri = redirectUri }) : Redirect(redirectUri);
    }

    private static string BuildRedirectUri(string redirectUri, string code, string? state)
    {
        var redirect = new UriBuilder(redirectUri);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(redirect.Query);
        var parameters = query.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value.ToString());
        parameters["code"] = code;
        if (!String.IsNullOrEmpty(state))
            parameters["state"] = state;

        redirect.Query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(String.Empty, parameters).TrimStart('?');
        return redirect.Uri.ToString();
    }

    private RedirectResult RedirectToAuthorizeBridge()
    {
        string authorizeUrl;
        if (String.IsNullOrWhiteSpace(Request.Query["resource"]))
        {
            var query = Request.Query.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value.ToString(), StringComparer.Ordinal);
            query["resource"] = GetMcpResource();
            authorizeUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/next/oauth/authorize", query);
        }
        else
        {
            authorizeUrl = "/next/oauth/authorize" + Request.QueryString;
        }

        return Redirect(authorizeUrl);
    }
}

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
            Resource = Resource
        };
    }
}

public sealed record OAuthAuthorizeResponse
{
    [JsonPropertyName("redirect_uri")]
    public required string RedirectUri { get; init; }
}

public sealed record OAuthAuthorizationServerMetadata
{
    [JsonPropertyName("issuer")]
    public required string Issuer { get; init; }

    [JsonPropertyName("authorization_endpoint")]
    public required string AuthorizationEndpoint { get; init; }

    [JsonPropertyName("token_endpoint")]
    public required string TokenEndpoint { get; init; }

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

    [FromForm(Name = "resource")]
    public string? Resource { get; init; }
}

public sealed record OAuthRevokeForm
{
    [FromForm(Name = "token")]
    public string? Token { get; init; }
}

public sealed record OAuthErrorResponse
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }
}
