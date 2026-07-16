using Exceptionless.Core.Services;
using Exceptionless.Web.Models.OAuth;

namespace Exceptionless.Web.Api.Messages;

public record GetAuthorizationServerMetadata;
public record GetMcpProtectedResourceMetadata;
public record GetRestApiProtectedResourceMetadata;
public record RedirectToAuthorizeBridge;
public record CompleteOAuthAuthorization(OAuthAuthorizeForm Form);
public record GetOAuthAuthorizeConsent(OAuthAuthorizeForm Form);
public record RegisterOAuthClient(OAuthClientRegistrationRequest Request);
public record IssueOAuthToken(OAuthTokenForm Form);
public record RevokeOAuthToken(OAuthRevokeForm Form);
