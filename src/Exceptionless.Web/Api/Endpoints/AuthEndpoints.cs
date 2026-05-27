using Exceptionless.Core.Authorization;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Models;
using IMediator = Foundatio.Mediator.IMediator;
using Microsoft.AspNetCore.Mvc;
using AuthMessages = Exceptionless.Web.Api.Messages;

namespace Exceptionless.Web.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2/auth")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("Auth");

        group.MapPost("login", async (IMediator mediator, IServiceProvider serviceProvider, HttpContext httpContext, [FromBody] Login model) =>
        {
            var validation = await ApiValidation.ValidateAsync(model, serviceProvider, StatusCodes.Status422UnprocessableEntity);
            if (validation is not null)
                return validation;

            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AuthMessages.LoginMessage(model, httpContext));
        })
        .AllowAnonymous()
        .Accepts<Login>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Login");

        group.MapGet("intercom", async (IMediator mediator, HttpContext httpContext)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AuthMessages.GetIntercomToken(httpContext)))
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Get the current user's Intercom messenger token.");

        group.MapGet("logout", async (IMediator mediator, HttpContext httpContext)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AuthMessages.LogoutMessage(httpContext)))
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .WithSummary("Logout the current user and remove the current access token");

        group.MapPost("signup", async (IMediator mediator, IServiceProvider serviceProvider, HttpContext httpContext, [FromBody] Signup model) =>
        {
            var validation = await ApiValidation.ValidateAsync(model, serviceProvider, StatusCodes.Status422UnprocessableEntity);
            if (validation is not null)
                return validation;

            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AuthMessages.SignupMessage(model, httpContext));
        })
        .AllowAnonymous()
        .Accepts<Signup>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Sign up");

        group.MapPost("github", async (IMediator mediator, IServiceProvider serviceProvider, HttpContext httpContext, [FromBody] ExternalAuthInfo value) =>
        {
            var validation = await ApiValidation.ValidateAsync(value, serviceProvider, StatusCodes.Status422UnprocessableEntity);
            if (validation is not null)
                return validation;

            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AuthMessages.GitHubLogin(value, httpContext));
        })
        .AllowAnonymous()
        .Accepts<ExternalAuthInfo>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Sign in with GitHub");

        group.MapPost("google", async (IMediator mediator, IServiceProvider serviceProvider, HttpContext httpContext, [FromBody] ExternalAuthInfo value) =>
        {
            var validation = await ApiValidation.ValidateAsync(value, serviceProvider, StatusCodes.Status422UnprocessableEntity);
            if (validation is not null)
                return validation;

            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AuthMessages.GoogleLogin(value, httpContext));
        })
        .AllowAnonymous()
        .Accepts<ExternalAuthInfo>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Sign in with Google");

        group.MapPost("facebook", async (IMediator mediator, IServiceProvider serviceProvider, HttpContext httpContext, [FromBody] ExternalAuthInfo value) =>
        {
            var validation = await ApiValidation.ValidateAsync(value, serviceProvider, StatusCodes.Status422UnprocessableEntity);
            if (validation is not null)
                return validation;

            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AuthMessages.FacebookLogin(value, httpContext));
        })
        .AllowAnonymous()
        .Accepts<ExternalAuthInfo>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Sign in with Facebook");

        group.MapPost("live", async (IMediator mediator, IServiceProvider serviceProvider, HttpContext httpContext, [FromBody] ExternalAuthInfo value) =>
        {
            var validation = await ApiValidation.ValidateAsync(value, serviceProvider, StatusCodes.Status422UnprocessableEntity);
            if (validation is not null)
                return validation;

            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AuthMessages.LiveLogin(value, httpContext));
        })
        .AllowAnonymous()
        .Accepts<ExternalAuthInfo>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Sign in with Microsoft");

        group.MapPost("unlink/{providerName:minlength(1)}", async (string providerName, IMediator mediator, HttpContext httpContext, [FromBody] ValueFromBody<string> providerUserId)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AuthMessages.RemoveExternalLogin(providerName, providerUserId, httpContext)))
        .Accepts<ValueFromBody<string>>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Removes an external login provider from the account");

        group.MapPost("change-password", async (IMediator mediator, IServiceProvider serviceProvider, HttpContext httpContext, [FromBody] ChangePasswordModel model) =>
        {
            var validation = await ApiValidation.ValidateAsync(model, serviceProvider, StatusCodes.Status422UnprocessableEntity);
            if (validation is not null)
                return validation;

            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AuthMessages.ChangePassword(model, httpContext));
        })
        .Accepts<ChangePasswordModel>("application/json", "application/*+json")
        .Produces<TokenResult>()
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Change password");

        group.MapGet("check-email-address/{email:minlength(1)}", async (string email, IMediator mediator, HttpContext httpContext)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AuthMessages.CheckEmailAddress(email, httpContext)))
        .AllowAnonymous()
        .ExcludeFromDescription();

        group.MapGet("forgot-password/{email:minlength(1)}", async (string email, IMediator mediator, HttpContext httpContext)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AuthMessages.ForgotPassword(email, httpContext)))
        .AllowAnonymous()
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Forgot password");

        group.MapPost("reset-password", async (IMediator mediator, IServiceProvider serviceProvider, HttpContext httpContext, [FromBody] ResetPasswordModel model) =>
        {
            var validation = await ApiValidation.ValidateAsync(model, serviceProvider, StatusCodes.Status422UnprocessableEntity);
            if (validation is not null)
                return validation;

            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AuthMessages.ResetPassword(model, httpContext));
        })
        .AllowAnonymous()
        .Accepts<ResetPasswordModel>("application/json", "application/*+json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Reset password");

        group.MapPost("cancel-reset-password/{token:minlength(1)}", async (string token, IMediator mediator, HttpContext httpContext)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AuthMessages.CancelResetPassword(token, httpContext)))
        .AllowAnonymous()
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Cancel reset password");

        return endpoints;
    }
}
