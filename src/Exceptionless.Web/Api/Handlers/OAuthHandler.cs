using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models.OAuth;
using Foundatio.Caching;
using Microsoft.AspNetCore.WebUtilities;
using HttpResults = Microsoft.AspNetCore.Http.Results;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Exceptionless.Web.Api.Handlers;

public sealed class OAuthHandler(
    OAuthService oauthService,
    AppOptions appOptions,
    ICacheClient cacheClient,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
{
    private HttpContext HttpContext => httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is unavailable.");

    public Task<IResult> Handle(GetAuthorizationServerMetadata message)
    {
        string origin = GetOrigin();
        return Task.FromResult<IResult>(HttpResults.Ok(new OAuthAuthorizationServerMetadata
        {
            Issuer = origin,
            AuthorizationEndpoint = $"{origin}/api/v2/oauth/authorize",
            TokenEndpoint = $"{origin}/api/v2/oauth/token",
            RegistrationEndpoint = $"{origin}/api/v2/oauth/register",
            RevocationEndpoint = $"{origin}/api/v2/oauth/revoke",
            GrantTypesSupported = [OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken],
            ResponseTypesSupported = ["code"],
            CodeChallengeMethodsSupported = [OAuthService.CodeChallengeMethod],
            TokenEndpointAuthMethodsSupported = ["none"],
            ScopesSupported = OAuthService.SupportedScopes,
            ResourceDocumentation = $"{origin}/mcp",
            ClientIdMetadataDocumentSupported = oauthService.ClientIdMetadataDocumentSupported
        }));
    }

    public Task<IResult> Handle(GetMcpProtectedResourceMetadata message)
    {
        return Task.FromResult(GetProtectedResourceMetadata(OAuthService.McpResource));
    }

    public Task<IResult> Handle(GetRestApiProtectedResourceMetadata message)
    {
        return Task.FromResult(GetProtectedResourceMetadata(OAuthService.RestApiResource));
    }

    public Task<IResult> Handle(RedirectToAuthorizeBridge message)
    {
        return Task.FromResult<IResult>(HttpResults.Redirect("/next/oauth/authorize" + HttpContext.Request.QueryString));
    }

    public Task<IResult> Handle(CompleteOAuthAuthorization message)
    {
        return CompleteAuthorizationAsync(message.Form.ToRequest());
    }

    public async Task<IResult> Handle(GetOAuthAuthorizeConsent message)
    {
        var request = message.Form.ToRequest();
        var validation = await ValidateAuthorizeRequestAsync(request);
        if (!validation.IsValid)
            return OAuthError(validation.Error, validation.ErrorDescription);

        return HttpResults.Ok(new OAuthAuthorizeConsentResponse
        {
            ClientId = validation.Client!.ClientId,
            ClientName = validation.Client.Name,
            RedirectUri = request.RedirectUri,
            Resource = validation.Resource!,
            Scopes = validation.Scopes,
            RequiredScopes = validation.ResourceDefinition!.RequiredScopes
        });
    }

    public async Task<IResult> Handle(RegisterOAuthClient message)
    {
        if (await IsDynamicClientRegistrationRateLimitedAsync())
        {
            return HttpResults.Json(new OAuthErrorResponse
            {
                Error = "temporarily_unavailable",
                ErrorDescription = "Too many dynamic client registration attempts."
            }, statusCode: StatusCodes.Status429TooManyRequests);
        }

        var result = await oauthService.RegisterClientAsync(message.Request);
        if (!result.IsSuccess)
            return OAuthError(result.Error, result.ErrorDescription);

        return HttpResults.Json(result.Response, statusCode: StatusCodes.Status201Created);
    }

    public async Task<IResult> Handle(IssueOAuthToken message)
    {
        var form = message.Form;
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

        return HttpResults.Ok(result.Response);
    }

    public async Task<IResult> Handle(RevokeOAuthToken message)
    {
        await oauthService.RevokeAsync(message.Form.Token, message.Form.ClientId);
        return HttpResults.Ok();
    }

    private IResult GetProtectedResourceMetadata(OAuthResourceDefinition resourceDefinition)
    {
        string origin = GetOrigin();
        string resource = OAuthService.CreateResourceUri(origin, resourceDefinition);
        return HttpResults.Ok(new OAuthProtectedResourceMetadata
        {
            Resource = resource,
            AuthorizationServers = [origin],
            ScopesSupported = resourceDefinition.Scopes,
            BearerMethodsSupported = ["header"],
            ResourceDocumentation = resource
        });
    }

    private async Task<bool> IsDynamicClientRegistrationRateLimitedAsync()
    {
        string cacheKey = $"ip:{HttpContext.Request.GetClientIpAddress()}:oauth-dcr:attempts";
        long attempts = await cacheClient.IncrementAsync(cacheKey, 1, timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromHours(1)));
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

    private static IResult OAuthError(string? error, string? description)
    {
        return HttpResults.BadRequest(new OAuthErrorResponse
        {
            Error = error ?? "invalid_request",
            ErrorDescription = description
        });
    }

    private async Task<AuthorizeRequestValidationResult> ValidateAuthorizeRequestAsync(OAuthAuthorizeRequest request)
    {
        if (!OAuthService.TryGetProtectedResource(request.Resource, GetOrigin(), out var resourceDefinition))
            return AuthorizeRequestValidationResult.Invalid("invalid_target", "The requested resource is not supported.");

        string resource = GetResource(resourceDefinition);
        var validation = await oauthService.ValidateAuthorizationRequestAsync(request, resource, resourceDefinition);
        if (!validation.IsValid)
            return AuthorizeRequestValidationResult.Invalid(validation.Error, validation.ErrorDescription);

        return AuthorizeRequestValidationResult.Valid(validation.Client!, validation.Scopes, resourceDefinition, resource);
    }

    private async Task<IResult> CompleteAuthorizationAsync(OAuthAuthorizeRequest request)
    {
        var validation = await ValidateAuthorizeRequestAsync(request);
        if (!validation.IsValid)
            return OAuthError(validation.Error, validation.ErrorDescription);

        var organizationValidation = ValidateRequestedOrganizations(request.OrganizationIds);
        if (!organizationValidation.IsValid)
            return OAuthError("invalid_request", organizationValidation.ErrorDescription);

        string code = await oauthService.CreateAuthorizationCodeAsync(request, HttpContext.Request.GetUser().Id, organizationValidation.OrganizationIds);
        string redirectUri = BuildRedirectUri(request.RedirectUri, code, request.State);
        return HttpResults.Ok(new OAuthAuthorizeResponse { RedirectUri = redirectUri });
    }

    private OrganizationValidationResult ValidateRequestedOrganizations(IReadOnlyCollection<string> requestedOrganizationIds)
    {
        var organizationIds = requestedOrganizationIds.Where(id => !String.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        if (organizationIds.Length == 0)
            return OrganizationValidationResult.Invalid("Select at least one organization.");

        var allowedOrganizationIds = HttpContext.Request.GetUser().OrganizationIds.ToHashSet(StringComparer.Ordinal);
        if (organizationIds.Any(id => !allowedOrganizationIds.Contains(id)))
            return OrganizationValidationResult.Invalid("One or more selected organizations are not available to the current user.");

        return OrganizationValidationResult.Valid(organizationIds);
    }

    private static string BuildRedirectUri(string redirectUri, string code, string? state)
    {
        var redirect = new UriBuilder(redirectUri);
        var query = QueryHelpers.ParseQuery(redirect.Query);
        var parameters = query.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value.ToString());
        parameters["code"] = code;
        if (!String.IsNullOrEmpty(state))
            parameters["state"] = state;

        redirect.Query = QueryHelpers.AddQueryString(String.Empty, parameters).TrimStart('?');
        return redirect.Uri.ToString();
    }
}

internal sealed record OrganizationValidationResult(bool IsValid, IReadOnlyCollection<string> OrganizationIds, string? ErrorDescription)
{
    public static OrganizationValidationResult Valid(IReadOnlyCollection<string> organizationIds) => new(true, organizationIds, null);
    public static OrganizationValidationResult Invalid(string errorDescription) => new(false, [], errorDescription);
}

internal sealed record AuthorizeRequestValidationResult(bool IsValid, OAuthClientOptions? Client, IReadOnlyCollection<string> Scopes, OAuthResourceDefinition? ResourceDefinition, string? Resource, string? Error, string? ErrorDescription)
{
    public static AuthorizeRequestValidationResult Valid(OAuthClientOptions client, IReadOnlyCollection<string> scopes, OAuthResourceDefinition resourceDefinition, string resource) => new(true, client, scopes, resourceDefinition, resource, null, null);
    public static AuthorizeRequestValidationResult Invalid(string? error, string? errorDescription) => new(false, null, [], null, null, error, errorDescription);
}
