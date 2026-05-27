using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using IMediator = Foundatio.Mediator.IMediator;
using Microsoft.AspNetCore.Mvc;
using UserMessages = Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Utility.OpenApi;

namespace Exceptionless.Web.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("User");

        group.MapGet("users/me", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.GetCurrentUser()))
        .Produces<ViewCurrentUser>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get current user")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["404"] = "The current user could not be found.",
            }
        });

        group.MapGet("users/{id:objectid}", async (string id, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.GetUserById(id)))
        .WithName("GetUserById")
        .Produces<ViewUser>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get by id")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the user.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The user could not be found.",
            }
        });

        group.MapGet("organizations/{organizationId:objectid}/users", async (string organizationId, IMediator mediator, int page = 1, int limit = 10)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.GetUsersByOrganization(organizationId, page, limit)))
        .Produces<IReadOnlyCollection<ViewUser>>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get by organization")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["organizationId"] = "The identifier of the organization.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The organization could not be found.",
            }
        });

        group.MapPatch("users/{id:objectid}", async (string id, IMediator mediator, [FromBody] Delta<UpdateUser> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.UpdateUserMessage(id, changes)))
        .Produces<ViewUser>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The changes",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the user.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the user.",
                ["404"] = "The user could not be found.",
            }
        });

        group.MapPut("users/{id:objectid}", async (string id, IMediator mediator, [FromBody] Delta<UpdateUser> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.UpdateUserMessage(id, changes)))
        .Produces<ViewUser>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The changes",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the user.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the user.",
                ["404"] = "The user could not be found.",
            }
        });

        group.MapDelete("users/me", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.DeleteCurrentUser()))
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete current user")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["404"] = "The current user could not be found.",
            }
        });

        group.MapDelete("users/{ids:objectids}", async (string ids, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.DeleteUsers(ids.FromDelimitedString())))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Remove")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["ids"] = "A comma-delimited list of user identifiers.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["400"] = "One or more validation errors occurred.",
                ["404"] = "One or more users were not found.",
                ["500"] = "An error occurred while deleting one or more users.",
            }
        });

        group.MapPost("users/{id:objectid}/email-address/{email:minlength(1)}", async (string id, string email, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.UpdateEmailAddress(id, email)))
        .Produces<UpdateEmailAddressResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .WithSummary("Update email address")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the user.",
                ["email"] = "The new email address.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the users email address.",
                ["422"] = "Validation error",
                ["429"] = "Update email address rate limit reached.",
            }
        });

        group.MapGet("users/verify-email-address/{token:token}", async (string token, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.VerifyEmailAddress(token)))
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Verify email address")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["token"] = "The token identifier.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The user could not be found.",
                ["422"] = "Verify Email Address Token has expired.",
            }
        });

        group.MapGet("users/{id:objectid}/resend-verification-email", async (string id, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new UserMessages.ResendVerificationEmail(id)))
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Resend verification email")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the user.",
            },
            ResponseDescriptions = new() {
                ["200"] = "The user verification email has been sent.",
                ["404"] = "The user could not be found.",
            }
        });

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
