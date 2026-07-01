using Exceptionless.Core.Authorization;
using Exceptionless.Core.Services;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Models.OAuth;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Api.Endpoints;

public static class OAuthEndpoints
{
    public static IEndpointRouteBuilder MapOAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(".well-known/oauth-authorization-server", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetAuthorizationServerMetadata()))
            .AllowAnonymous()
            .Produces<OAuthAuthorizationServerMetadata>();

        endpoints.MapGet(".well-known/oauth-protected-resource/mcp", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetMcpProtectedResourceMetadata()))
            .AllowAnonymous()
            .Produces<OAuthProtectedResourceMetadata>();

        endpoints.MapGet(".well-known/oauth-protected-resource/api/v2", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetRestApiProtectedResourceMetadata()))
            .AllowAnonymous()
            .Produces<OAuthProtectedResourceMetadata>();

        var group = endpoints.MapGroup("api/v2/oauth")
            .WithTags("OAuth");

        group.MapGet("authorize", async (
            IMediator mediator,
            [FromQuery(Name = "client_id")] string? clientId = null,
            [FromQuery(Name = "response_type")] string? responseType = null,
            [FromQuery(Name = "redirect_uri")] string? redirectUri = null,
            [FromQuery] string? scope = null,
            [FromQuery] string? state = null,
            [FromQuery(Name = "code_challenge")] string? codeChallenge = null,
            [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod = null,
            [FromQuery] string? resource = null)
                => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new RedirectToAuthorizeBridge()))
            .AllowAnonymous()
            .Produces(StatusCodes.Status302Found);

        group.MapPost("authorize", async (IMediator mediator, [FromBody] OAuthAuthorizeForm form)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new CompleteOAuthAuthorization(form)))
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .Accepts<OAuthAuthorizeForm>("application/json", "application/*+json")
            .Produces<OAuthAuthorizeResponse>()
            .Produces<OAuthErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapPost("authorize/consent", async (IMediator mediator, [FromBody] OAuthAuthorizeForm form)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetOAuthAuthorizeConsent(form)))
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .Accepts<OAuthAuthorizeForm>("application/json", "application/*+json")
            .Produces<OAuthAuthorizeConsentResponse>()
            .Produces<OAuthErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapPost("register", async (IMediator mediator, [FromBody] OAuthClientRegistrationRequest request)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new RegisterOAuthClient(request)))
            .AllowAnonymous()
            .Accepts<OAuthClientRegistrationRequest>("application/json", "application/*+json")
            .Produces<OAuthClientRegistrationResponse>(StatusCodes.Status201Created)
            .Produces<OAuthErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<OAuthErrorResponse>(StatusCodes.Status429TooManyRequests);

        group.MapPost("token", IssueTokenAsync)
            .AllowAnonymous()
            .Accepts<OAuthTokenForm>("application/x-www-form-urlencoded")
            .Produces<OAuthTokenResponse>()
            .Produces<OAuthErrorResponse>(StatusCodes.Status400BadRequest)
            .DisableAntiforgery();

        group.MapPost("revoke", RevokeTokenAsync)
            .AllowAnonymous()
            .Accepts<OAuthRevokeForm>("application/x-www-form-urlencoded")
            .Produces(StatusCodes.Status200OK)
            .DisableAntiforgery();

        return endpoints;
    }

    private static async Task<Microsoft.AspNetCore.Http.IResult> IssueTokenAsync(IMediator mediator, HttpRequest request, CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var tokenForm = new OAuthTokenForm
        {
            GrantType = GetFormValue(form, "grant_type") ?? String.Empty,
            Code = GetFormValue(form, "code"),
            RedirectUri = GetFormValue(form, "redirect_uri"),
            ClientId = GetFormValue(form, "client_id"),
            CodeVerifier = GetFormValue(form, "code_verifier"),
            RefreshToken = GetFormValue(form, "refresh_token"),
            Resource = GetFormValue(form, "resource")
        };

        return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new IssueOAuthToken(tokenForm));
    }

    private static async Task<Microsoft.AspNetCore.Http.IResult> RevokeTokenAsync(IMediator mediator, HttpRequest request, CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var revokeForm = new OAuthRevokeForm
        {
            Token = GetFormValue(form, "token"),
            ClientId = GetFormValue(form, "client_id")
        };

        return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new RevokeOAuthToken(revokeForm));
    }

    private static string? GetFormValue(IFormCollection form, string key)
    {
        string value = form[key].ToString();
        return String.IsNullOrEmpty(value) ? null : value;
    }
}
