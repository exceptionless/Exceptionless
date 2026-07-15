using Exceptionless.Web.Models;

namespace Exceptionless.Web.Api.Messages;

public record LoginMessage(Login Model, HttpContext Context);
public record GetIntercomToken(HttpContext Context);
public record LogoutMessage(HttpContext Context);
public record SignupMessage(Signup Model, HttpContext Context);
public record GitHubLogin(ExternalAuthInfo AuthInfo, HttpContext Context);
public record GoogleLogin(ExternalAuthInfo AuthInfo, HttpContext Context);
public record FacebookLogin(ExternalAuthInfo AuthInfo, HttpContext Context);
public record LiveLogin(ExternalAuthInfo AuthInfo, HttpContext Context);
public record RemoveExternalLogin(string ProviderName, ValueFromBody<string> ProviderUserId, HttpContext Context);
public record ChangePassword(ChangePasswordModel Model, HttpContext Context);
public record CheckEmailAddress(string Email, HttpContext Context);
public record ForgotPassword(string Email, HttpContext Context);
public record ResetPassword(ResetPasswordModel Model, HttpContext Context);
public record CancelResetPassword(string Token, HttpContext Context);
