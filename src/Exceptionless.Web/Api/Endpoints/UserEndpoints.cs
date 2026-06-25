using System.Text.Json;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Storage;
using Foundatio.Mediator;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UserMessages = Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Utility.OpenApi;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
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

        group.MapGet("users/me", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<ViewCurrentUser>>(new UserMessages.GetCurrentUser())).ToHttpResult(resultMapper))
        .Produces<ViewCurrentUser>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get current user")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["404"] = "The current user could not be found.",
            }
        });

        group.MapGet("users/{id:objectid}", async (string id, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
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

        group.MapGet("organizations/{organizationId:objectid}/users", async (string organizationId, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, int page = 1, int limit = 10)
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

        group.MapPatch("users/{id:objectid}", UpdateUserAsync)
        .WithDisplayName("HTTP: PATCH api/v2/users/{id:objectid}")
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
        })
        .WithMetadata(new JsonPatchRequestBodyAttribute<UpdateUser>());

        group.MapPut("users/{id:objectid}", UpdateUserAsync)
        .WithDisplayName("HTTP: PUT api/v2/users/{id:objectid}")
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
        })
        .WithMetadata(new JsonPatchRequestBodyAttribute<UpdateUser>());

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
                ["file"] = "The avatar image file.",
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
        .WithMetadata(new ResponseCacheAttribute { Duration = 31536000, Location = ResponseCacheLocation.Any })
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

        group.MapDelete("users/me", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
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

        group.MapDelete("users/{ids:objectids}", async (string ids, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
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

        group.MapPost("users/{id:objectid}/email-address/{email:minlength(1)}", async (string id, string email, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
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

        group.MapGet("users/verify-email-address/{token:token}", async (string token, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
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

        group.MapGet("users/{id:objectid}/resend-verification-email", async (string id, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
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

        group.MapPost("users/unverify-email-address", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new UserMessages.UnverifyEmailAddresses())).ToHttpResult(resultMapper))
        .Accepts<string>("text/plain")
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ExcludeFromDescription();

        group.MapPost("users/{id:objectid}/admin-role", async (string id, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new UserMessages.AddAdminRole(id))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapDelete("users/{id:objectid}/admin-role", async (string id, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new UserMessages.RemoveAdminRole(id))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<HttpIResult> UpdateUserAsync(
        string id,
        IMediator mediator,
        IMediatorResultMapper<HttpIResult> resultMapper,
        IOptions<HttpJsonOptions> jsonOptions,
        [FromBody] JsonElement body)
    {
        var patchDocument = JsonPatchValidation.FromJsonBody<UpdateUser>(body, jsonOptions.Value.SerializerOptions);
        if (patchDocument is null)
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["patch"] = ["Invalid patch document."]
            });
        }

        return (await mediator.InvokeAsync<Result<object>>(new UserMessages.UpdateUserMessage(id, patchDocument))).ToHttpResult(resultMapper);
    }

    private static async Task<HttpIResult> UploadAvatarAsync(string id, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, [FromServices] IFileStorage fileStorage, [FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
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

    private static async Task<HttpIResult> DeleteAvatarAsync(string id, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, [FromServices] IFileStorage fileStorage, CancellationToken cancellationToken)
    {
        var result = await mediator.InvokeAsync<Result<ProfileImageUpdate<object>>>(new UserMessages.DeleteUserAvatar(id));
        if (!result.IsSuccess)
            return result.ToHttpResult(resultMapper);

        var update = result.ValueOrDefault!;
        await ProfileImageStorage.DeleteAsync(fileStorage, update.PreviousFileName, "users", id, cancellationToken);
        return HttpResults.Ok(update.View);
    }

    private static async Task<HttpIResult> GetAvatarAsync(string id, string fileName, [FromServices] IFileStorage fileStorage, CancellationToken cancellationToken)
    {
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
