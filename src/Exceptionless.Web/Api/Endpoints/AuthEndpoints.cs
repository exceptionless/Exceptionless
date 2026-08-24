using Exceptionless.Core.Authorization;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Models;
using Foundatio.Mediator;
using HttpIResult = Microsoft.AspNetCore.Http.IResult;
using Microsoft.AspNetCore.Mvc;
using AuthMessages = Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Utility.OpenApi;

namespace Exceptionless.Web.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2/auth")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("Auth");

        group.MapPost("login", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, HttpContext httpContext, [FromBody] Login model) =>
        {
            return (await mediator.InvokeAsync<Result<TokenResult>>(new AuthMessages.LoginMessage(model, httpContext))).ToHttpResult(resultMapper);
        })
        .AllowAnonymous()
        .Accepts<Login>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Login")
        .WithDescription("""
            Log in with your email address and password to generate a token scoped with your users roles.

            ```{ "email": "noreply@exceptionless.io", "password": "exceptionless" }```

            This token can then be used to access the api. You can use this token in the header (bearer authentication)
            or append it onto the query string: ?access_token=MY_TOKEN

            Please note that you can also use this token on the documentation site by placing it in the
            headers api_key input box.
            """)
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["200"] = "User Authentication Token",
                ["401"] = "Login failed",
                ["422"] = "Validation error",
            }
        });

        group.MapGet("intercom", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, HttpContext httpContext)
            => (await mediator.InvokeAsync<Result<TokenResult>>(new AuthMessages.GetIntercomToken(httpContext))).ToHttpResult(resultMapper))
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Get the current user's Intercom messenger token.")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["200"] = "Intercom messenger token",
                ["401"] = "User not logged in",
                ["422"] = "Intercom is not enabled.",
            }
        });

        group.MapGet("logout", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, HttpContext httpContext)
            => (await mediator.InvokeAsync<Result>(new AuthMessages.LogoutMessage(httpContext))).ToHttpResult(resultMapper))
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .WithSummary("Logout the current user and remove the current access token")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["200"] = "User successfully logged-out",
                ["401"] = "User not logged in",
                ["403"] = "Current action is not supported with user access token",
            }
        });

        group.MapPost("signup", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, HttpContext httpContext, [FromBody] Signup model) =>
        {
            return (await mediator.InvokeAsync<Result<TokenResult>>(new AuthMessages.SignupMessage(model, httpContext))).ToHttpResult(resultMapper);
        })
        .AllowAnonymous()
        .Accepts<Signup>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Sign up")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["200"] = "User Authentication Token",
                ["401"] = "Sign-up failed",
                ["403"] = "Account Creation is currently disabled",
                ["422"] = "Validation error",
            }
        });

        group.MapPost("github", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, HttpContext httpContext, [FromBody] ExternalAuthInfo value) =>
        {
            return (await mediator.InvokeAsync<Result<TokenResult>>(new AuthMessages.GitHubLogin(value, httpContext))).ToHttpResult(resultMapper);
        })
        .AllowAnonymous()
        .Accepts<ExternalAuthInfo>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Sign in with GitHub")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["200"] = "User Authentication Token",
                ["403"] = "Account Creation is currently disabled",
                ["422"] = "Validation error",
            }
        });

        group.MapPost("google", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, HttpContext httpContext, [FromBody] ExternalAuthInfo value) =>
        {
            return (await mediator.InvokeAsync<Result<TokenResult>>(new AuthMessages.GoogleLogin(value, httpContext))).ToHttpResult(resultMapper);
        })
        .AllowAnonymous()
        .Accepts<ExternalAuthInfo>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Sign in with Google")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["200"] = "User Authentication Token",
                ["403"] = "Account Creation is currently disabled",
                ["422"] = "Validation error",
            }
        });

        group.MapPost("facebook", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, HttpContext httpContext, [FromBody] ExternalAuthInfo value) =>
        {
            return (await mediator.InvokeAsync<Result<TokenResult>>(new AuthMessages.FacebookLogin(value, httpContext))).ToHttpResult(resultMapper);
        })
        .AllowAnonymous()
        .Accepts<ExternalAuthInfo>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Sign in with Facebook")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["200"] = "User Authentication Token",
                ["403"] = "Account Creation is currently disabled",
                ["422"] = "Validation error",
            }
        });

        group.MapPost("live", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, HttpContext httpContext, [FromBody] ExternalAuthInfo value) =>
        {
            return (await mediator.InvokeAsync<Result<TokenResult>>(new AuthMessages.LiveLogin(value, httpContext))).ToHttpResult(resultMapper);
        })
        .AllowAnonymous()
        .Accepts<ExternalAuthInfo>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Sign in with Microsoft")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["200"] = "User Authentication Token",
                ["403"] = "Account Creation is currently disabled",
                ["422"] = "Validation error",
            }
        });

        group.MapPost("unlink/{providerName:minlength(1)}", async (string providerName, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, HttpContext httpContext, [FromBody] ValueFromBody<string> providerUserId)
            => (await mediator.InvokeAsync<Result<TokenResult>>(new AuthMessages.RemoveExternalLogin(providerName, providerUserId, httpContext))).ToHttpResult(resultMapper))
        .Accepts<ValueFromBody<string>>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Removes an external login provider from the account")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The provider user id.",
            ParameterDescriptions = new() {
                ["providerName"] = "The provider name.",
            },
            ResponseDescriptions = new() {
                ["200"] = "User Authentication Token",
                ["400"] = "Invalid provider name.",
            }
        });

        group.MapPost("change-password", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, HttpContext httpContext, [FromBody] ChangePasswordModel model) =>
        {
            return (await mediator.InvokeAsync<Result<TokenResult>>(new AuthMessages.ChangePassword(model, httpContext))).ToHttpResult(resultMapper);
        })
        .Accepts<ChangePasswordModel>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Change password")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["200"] = "User Authentication Token",
                ["422"] = "Validation error",
            }
        });

        group.MapGet("check-email-address/{email:minlength(1)}", async (string email, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, HttpContext httpContext)
            => (await mediator.InvokeAsync<Result>(new AuthMessages.CheckEmailAddress(email, httpContext))).ToHttpResult(resultMapper))
        .AllowAnonymous()
        .ExcludeFromDescription();

        group.MapGet("forgot-password/{email:minlength(1)}", async (string email, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, HttpContext httpContext)
            => (await mediator.InvokeAsync<Result>(new AuthMessages.ForgotPassword(email, httpContext))).ToHttpResult(resultMapper))
        .AllowAnonymous()
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Forgot password")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["email"] = "The email address.",
            },
            ResponseDescriptions = new() {
                ["200"] = "Forgot password email was sent.",
                ["400"] = "Invalid email address.",
            }
        });

        group.MapPost("reset-password", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, HttpContext httpContext, [FromBody] ResetPasswordModel model) =>
        {
            return (await mediator.InvokeAsync<Result>(new AuthMessages.ResetPassword(model, httpContext))).ToHttpResult(resultMapper);
        })
        .AllowAnonymous()
        .Accepts<ResetPasswordModel>("application/json", "application/*+json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Reset password")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["200"] = "Password reset email was sent.",
                ["422"] = "Invalid reset password model.",
            }
        });

        group.MapPost("cancel-reset-password/{token:minlength(1)}", async (string token, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, HttpContext httpContext) =>
        {
            var contentTypeResult = ApiValidation.ValidateJsonContentType(httpContext.Request);
            if (contentTypeResult is not null)
                return contentTypeResult;

            return (await mediator.InvokeAsync<Result>(new AuthMessages.CancelResetPassword(token, httpContext))).ToHttpResult(resultMapper);
        })
        .AllowAnonymous()
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Cancel reset password")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["token"] = "The password reset token.",
            },
            ResponseDescriptions = new() {
                ["200"] = "Password reset email was cancelled.",
                ["400"] = "Invalid password reset token.",
            }
        });

        return endpoints;
    }
}
