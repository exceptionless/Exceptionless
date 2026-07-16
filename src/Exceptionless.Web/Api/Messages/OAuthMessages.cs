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
public record CreateDeviceAuthorization(OAuthDeviceAuthorizationForm Form);
public record RedirectToDeviceBridge(string? UserCode);
public record GetDeviceConsent(OAuthDeviceConsentForm Form);
public record ApproveDeviceAuthorization(OAuthDeviceAuthorizeForm Form);
public record DenyDeviceAuthorization(OAuthDeviceConsentForm Form);
public record IssueOAuthToken(OAuthTokenForm Form);
public record RevokeOAuthToken(OAuthRevokeForm Form);
