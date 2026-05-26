using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using IMediator = Foundatio.Mediator.IMediator;
using Microsoft.AspNetCore.Mvc;
using UserMessages = Exceptionless.Web.Api.Messages;

namespace Exceptionless.Web.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .WithTags("Users");

        group.MapGet("users/me", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.GetCurrentUser()))
        .Produces<ViewCurrentUser>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("users/{id:objectid}", async (string id, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.GetUserById(id)))
        .WithName("GetUserById")
        .Produces<ViewUser>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("organizations/{organizationId:objectid}/users", async (string organizationId, IMediator mediator, int page = 1, int limit = 10)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.GetUsersByOrganization(organizationId, page, limit)))
        .Produces<IReadOnlyCollection<ViewUser>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("users/{id:objectid}", async (string id, IMediator mediator, [FromBody] Delta<UpdateUser> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.UpdateUserMessage(id, changes)))
        .Produces<ViewUser>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("users/{id:objectid}", async (string id, IMediator mediator, [FromBody] Delta<UpdateUser> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.UpdateUserMessage(id, changes)))
        .Produces<ViewUser>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("users/me", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.DeleteCurrentUser()))
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("users/{ids:objectids}", async (string ids, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.DeleteUsers(ids.FromDelimitedString())))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("users/{id:objectid}/email-address/{email:minlength(1)}", async (string id, string email, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.UpdateEmailAddress(id, email)))
        .Produces<UpdateEmailAddressResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("users/verify-email-address/{token:token}", async (string token, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.VerifyEmailAddress(token)))
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("users/{id:objectid}/resend-verification-email", async (string id, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.ResendVerificationEmail(id)))
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("users/unverify-email-address", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.UnverifyEmailAddresses()))
        .Accepts<string>("text/plain")
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ExcludeFromDescription();

        group.MapPost("users/{id:objectid}/admin-role", async (string id, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.AddAdminRole(id)))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapDelete("users/{id:objectid}/admin-role", async (string id, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.RemoveAdminRole(id)))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        return endpoints;
    }
}
