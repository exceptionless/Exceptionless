using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using Exceptionless.Web.Models.OAuth;
using Exceptionless.Web.Utility;
using Foundatio.Storage;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UserMessages = Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Utility.OpenApi;
using HttpIResult = Microsoft.AspNetCore.Http.IResult;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Exceptionless.Web.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("User");

        group.MapGet("users/me", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<ViewCurrentUser>>(new UserMessages.GetCurrentUser())).ToHttpResult(resultMapper))
        .Produces<ViewCurrentUser>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get current user")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["404"] = "The current user could not be found.",
            }
        });

        group.MapGet("users/me/oauth-grants", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<IReadOnlyCollection<ViewOAuthGrant>>>(new UserMessages.GetCurrentUserOAuthGrants())).ToHttpResult(resultMapper))
        .Produces<IReadOnlyCollection<ViewOAuthGrant>>()
        .WithSummary("Get current user OAuth grants");

        group.MapDelete("users/me/oauth-grants/{id:minlength(1)}", async (string id, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new UserMessages.RevokeCurrentUserOAuthGrant(id))).ToHttpResult(resultMapper))
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Revoke current user OAuth grant")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The OAuth grant identifier.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The OAuth grant could not be found.",
            }
        });

        group.MapGet("users/{id:objectid}", async (string id, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<object>>(new UserMessages.GetUserById(id))).ToHttpResult(resultMapper))
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

        group.MapGet("organizations/{organizationId:objectid}/users", async (string organizationId, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, int page = 1, int limit = 10)
            => (await mediator.InvokeAsync<Result<PagedResult<ViewUser>>>(new UserMessages.GetUsersByOrganization(organizationId, page, limit))).ToHttpResult(resultMapper))
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

        group.MapPatch("users/{id:objectid}", async (string id, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromBody] Delta<UpdateUser>? changes)
            => changes is null ? ApiValidation.MissingRequestBody() : (await mediator.InvokeAsync<Result<object>>(new UserMessages.UpdateUserMessage(id, changes))).ToHttpResult(resultMapper))
        .Accepts<Delta<UpdateUser>>(false, "application/json", "application/*+json")
        .Produces<ViewUser>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The changes",
            RequestBodyRequired = true,
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the user.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the user.",
                ["404"] = "The user could not be found.",
            }
        });

        group.MapPut("users/{id:objectid}", async (string id, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromBody] Delta<UpdateUser>? changes)
            => changes is null ? ApiValidation.MissingRequestBody() : (await mediator.InvokeAsync<Result<object>>(new UserMessages.UpdateUserMessage(id, changes))).ToHttpResult(resultMapper))
        .Accepts<Delta<UpdateUser>>(false, "application/json", "application/*+json")
        .Produces<ViewUser>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The changes",
            RequestBodyRequired = true,
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the user.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the user.",
                ["404"] = "The user could not be found.",
            }
        });

        group.MapPost("users/{id:objectid}/avatar", UploadAvatarAsync)
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<ViewUser>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithMetadata(
            new MultipartFileUploadAttribute(),
            new RequestSizeLimitAttribute(ProfileImageStorage.MaxRequestBodySize),
            new RequestFormLimitsAttribute { MultipartBodyLengthLimit = ProfileImageStorage.MaxRequestBodySize })
        .WithSummary("Upload avatar")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the user.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The user could not be found.",
                ["422"] = "The image file is invalid.",
            }
        })
        .DisableAntiforgery();

        group.MapDelete("users/{id:objectid}/avatar", DeleteAvatarAsync)
        .Produces<ViewUser>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Remove avatar")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the user.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The user could not be found.",
            }
        });

        group.MapGet("users/{id:objectid}/avatar/{fileName}", GetAvatarAsync)
        .AllowAnonymous()
        .WithName("GetUserAvatar")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get avatar")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the user.",
                ["fileName"] = "The avatar file name.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The avatar could not be found.",
            }
        });

        group.MapDelete("users/me", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<ModelActionResults>>(new UserMessages.DeleteCurrentUser())).ToHttpResult(resultMapper))
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete current user")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["404"] = "The current user could not be found.",
            }
        });

        group.MapDelete("users/{ids:objectids}", async (string ids, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<ModelActionResults>>(new UserMessages.DeleteUsers(ids.FromDelimitedString()))).ToHttpResult(resultMapper))
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

        group.MapPost("users/{id:objectid}/email-address/{email:minlength(1)}", async (string id, string email, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<UpdateEmailAddressResult>>(new UserMessages.UpdateEmailAddress(id, email))).ToHttpResult(resultMapper))
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

        group.MapGet("users/verify-email-address/{token:token}", async (string token, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new UserMessages.VerifyEmailAddress(token))).ToHttpResult(resultMapper))
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

        group.MapGet("users/{id:objectid}/resend-verification-email", async (string id, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new UserMessages.ResendVerificationEmail(id))).ToHttpResult(resultMapper))
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

        group.MapPost("users/unverify-email-address", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper) =>
        {
            var contentTypeResult = ApiValidation.ValidateContentType(httpContext.Request, "text/plain");
            if (contentTypeResult is not null)
                return contentTypeResult;

            return (await mediator.InvokeAsync<Result>(new UserMessages.UnverifyEmailAddresses())).ToHttpResult(resultMapper);
        })
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ExcludeFromDescription();

        group.MapPost("users/{id:objectid}/admin-role", async (string id, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new UserMessages.AddAdminRole(id))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapDelete("users/{id:objectid}/admin-role", async (string id, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new UserMessages.RemoveAdminRole(id))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<HttpIResult> UploadAvatarAsync(string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromServices] IFileStorage fileStorage, CancellationToken cancellationToken)
    {
        var accessResult = await mediator.InvokeAsync<Result<object>>(new UserMessages.GetUserById(id));
        if (!accessResult.IsSuccess)
            return accessResult.ToHttpResult(resultMapper);

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        var modelState = new ModelStateDictionary();
        var image = await ProfileImageStorage.SaveAsync(fileStorage, file, "users", id, modelState, cancellationToken);
        if (image is null)
            return ValidationProblem(modelState);

        try
        {
            var result = await mediator.InvokeAsync<Result<ProfileImageUpdate<object>>>(new UserMessages.SetUserAvatar(id, image.FileName));
            if (!result.IsSuccess)
            {
                await ProfileImageStorage.TryDeleteAsync(fileStorage, image.FileName, "users", id, CancellationToken.None);
                return result.ToHttpResult(resultMapper);
            }

            var update = result.ValueOrDefault!;
            await ProfileImageStorage.DeleteAsync(fileStorage, update.PreviousFileName, "users", id, cancellationToken);
            return HttpResults.Ok(update.View);
        }
        catch
        {
            await ProfileImageStorage.TryDeleteAsync(fileStorage, image.FileName, "users", id, CancellationToken.None);
            throw;
        }
    }

    private static async Task<HttpIResult> DeleteAvatarAsync(string id, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromServices] IFileStorage fileStorage, CancellationToken cancellationToken)
    {
        var result = await mediator.InvokeAsync<Result<ProfileImageUpdate<object>>>(new UserMessages.DeleteUserAvatar(id));
        if (!result.IsSuccess)
            return result.ToHttpResult(resultMapper);

        var update = result.ValueOrDefault!;
        await ProfileImageStorage.DeleteAsync(fileStorage, update.PreviousFileName, "users", id, cancellationToken);
        return HttpResults.Ok(update.View);
    }

    private static async Task<HttpIResult> GetAvatarAsync(string id, string fileName, HttpContext httpContext, [FromServices] IFileStorage fileStorage, CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = ProfileImageStorage.PublicCacheControl;

        if (!ProfileImageStorage.TryGetContentType(fileName, out string contentType))
            return HttpResults.NotFound();

        var stream = await ProfileImageStorage.GetFileStreamAsync(fileStorage, fileName, "users", id, cancellationToken);
        return stream is null ? HttpResults.NotFound() : HttpResults.File(stream, contentType);
    }

    private static HttpIResult ValidationProblem(ModelStateDictionary modelState)
    {
        var errors = modelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        return HttpResults.ValidationProblem(errors, statusCode: StatusCodes.Status422UnprocessableEntity);
    }
}
