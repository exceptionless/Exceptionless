using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models.OAuth;
using Foundatio.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Controllers;

[Route("")]
public sealed class OAuthController(OAuthService oauthService, AppOptions appOptions, ICacheClient cacheClient, TimeProvider timeProvider) : ExceptionlessApiController(timeProvider)
{
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

    [HttpGet(".well-known/oauth-protected-resource/mcp")]
    [AllowAnonymous]
    public ActionResult<OAuthProtectedResourceMetadata> GetMcpProtectedResourceMetadataAsync()
    {
        return GetProtectedResourceMetadata(OAuthService.McpResource);
    }

    [HttpGet(".well-known/oauth-protected-resource/api/v2")]
    [AllowAnonymous]
    public ActionResult<OAuthProtectedResourceMetadata> GetRestApiProtectedResourceMetadataAsync()
    {
        return GetProtectedResourceMetadata(OAuthService.RestApiResource);
    }

    private ActionResult<OAuthProtectedResourceMetadata> GetProtectedResourceMetadata(OAuthResourceDefinition resourceDefinition)
    {
        string origin = GetOrigin();
        string resource = OAuthService.CreateResourceUri(origin, resourceDefinition);
        return Ok(new OAuthProtectedResourceMetadata
        {
            Resource = resource,
            AuthorizationServers = [origin],
            ScopesSupported = resourceDefinition.Scopes,
            BearerMethodsSupported = ["header"],
            ResourceDocumentation = resource
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
            Resource = form.Resource
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
        return attempts > appOptions.OAuthServerOptions.DynamicClientRegistrationIpLimit;
    }

    private string GetOrigin()
    {
        return new Uri(appOptions.BaseURL).GetLeftPart(UriPartial.Authority);
    }

    private string GetResource(OAuthResourceDefinition resourceDefinition)
    {
        return OAuthService.CreateResourceUri(GetOrigin(), resourceDefinition);
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
        if (!OAuthService.TryGetProtectedResource(request.Resource, GetOrigin(), out var resourceDefinition))
            return OAuthError("invalid_target", "The requested resource is not supported.");

        var validation = await oauthService.ValidateAuthorizationRequestAsync(request, GetResource(resourceDefinition), resourceDefinition);
        if (!validation.IsValid)
            return OAuthError(validation.Error, validation.ErrorDescription);

        var organizationValidation = ValidateRequestedOrganizations(request.OrganizationIds);
        if (!organizationValidation.IsValid)
            return OAuthError("invalid_request", organizationValidation.ErrorDescription);

        string code = await oauthService.CreateAuthorizationCodeAsync(request, CurrentUser.Id, organizationValidation.OrganizationIds);
        string redirectUri = BuildRedirectUri(request.RedirectUri, code, request.State);
        return jsonResponse ? Ok(new OAuthAuthorizeResponse { RedirectUri = redirectUri }) : Redirect(redirectUri);
    }

    private OrganizationValidationResult ValidateRequestedOrganizations(IReadOnlyCollection<string> requestedOrganizationIds)
    {
        var organizationIds = requestedOrganizationIds.Where(id => !String.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        if (organizationIds.Length == 0)
            return OrganizationValidationResult.Invalid("Select at least one organization.");

        var allowedOrganizationIds = CurrentUser.OrganizationIds.ToHashSet(StringComparer.Ordinal);
        if (organizationIds.Any(id => !allowedOrganizationIds.Contains(id)))
            return OrganizationValidationResult.Invalid("One or more selected organizations are not available to the current user.");

        return OrganizationValidationResult.Valid(organizationIds);
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
        return Redirect("/next/oauth/authorize" + Request.QueryString);
    }
}

internal sealed record OrganizationValidationResult(bool IsValid, IReadOnlyCollection<string> OrganizationIds, string? ErrorDescription)
{
    public static OrganizationValidationResult Valid(IReadOnlyCollection<string> organizationIds) => new(true, organizationIds, null);
    public static OrganizationValidationResult Invalid(string errorDescription) => new(false, [], errorDescription);
}
