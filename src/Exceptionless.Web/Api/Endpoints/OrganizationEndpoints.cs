using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Storage;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrganizationMessages = Exceptionless.Web.Api.Messages;
using Invoice = Exceptionless.Web.Models.Invoice;
using Exceptionless.Web.Utility.OpenApi;
using HttpIResult = Microsoft.AspNetCore.Http.IResult;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Exceptionless.Web.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("Organization");

        group.MapGet("organizations", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, string? filter = null, string? mode = null)
            => (await mediator.InvokeAsync<Result<IReadOnlyCollection<ViewOrganization>>>(new OrganizationMessages.GetOrganizations(filter, mode, httpContext))).ToHttpResult(resultMapper))
        .Produces<IReadOnlyCollection<ViewOrganization>>()
        .WithSummary("Get all")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["mode"] = "If no mode is set then a lightweight organization object will be returned. If the mode is set to stats than the fully populated object will be returned.",
            }
        });

        group.MapGet("admin/organizations", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, string? criteria = null, bool? paid = null, bool? suspended = null, string? mode = null, int page = 1, int limit = 10, OrganizationSortBy sort = OrganizationSortBy.Newest)
            => (await mediator.InvokeAsync<Result<PagedResult<ViewOrganization>>>(new OrganizationMessages.GetAdminOrganizations(criteria, paid, suspended, mode, page, limit, sort, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces<IReadOnlyCollection<ViewOrganization>>()
        .ExcludeFromDescription();

        group.MapGet("admin/organizations/stats", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<BillingPlanStats>>(new OrganizationMessages.GetOrganizationPlanStats(httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces<BillingPlanStats>()
        .ExcludeFromDescription();

        group.MapGet("organizations/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, string? mode = null)
            => (await mediator.InvokeAsync<Result<ViewOrganization>>(new OrganizationMessages.GetOrganizationById(id, mode, httpContext))).ToHttpResult(resultMapper))
        .WithName("GetOrganizationById")
        .Produces<ViewOrganization>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get by id")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the organization.",
                ["mode"] = "If no mode is set then the a lightweight organization object will be returned. If the mode is set to stats than the fully populated object will be returned.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The organization could not be found.",
            }
        });

        group.MapPost("organizations", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromBody] NewOrganization organization) =>
        {
            return (await mediator.InvokeAsync<Result<ViewOrganization>>(new OrganizationMessages.CreateOrganization(organization, httpContext))).ToHttpResult(resultMapper);
        })
        .Accepts<NewOrganization>("application/json", "application/*+json")
        .Produces<ViewOrganization>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Create")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The organization.",
            ResponseDescriptions = new() {
                ["201"] = "Created",
                ["400"] = "An error occurred while creating the organization.",
                ["409"] = "The organization already exists.",
            }
        });

        group.MapPatch("organizations/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromBody] Delta<NewOrganization>? changes) =>
        {
            if (changes is null)
                return ApiValidation.MissingRequestBody();

            return (await mediator.InvokeAsync<Result<ViewOrganization>>(new OrganizationMessages.UpdateOrganizationMessage(id, changes, httpContext))).ToHttpResult(resultMapper);
        })
        .Accepts<Delta<NewOrganization>>("application/json", "application/*+json")
        .Produces<ViewOrganization>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Update")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyRequired = true,
            RequestBodyDescription = "The changes",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the organization.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the organization.",
                ["404"] = "The organization could not be found.",
            }
        });

        group.MapPut("organizations/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromBody] Delta<NewOrganization>? changes) =>
        {
            if (changes is null)
                return ApiValidation.MissingRequestBody();

            return (await mediator.InvokeAsync<Result<ViewOrganization>>(new OrganizationMessages.UpdateOrganizationMessage(id, changes, httpContext))).ToHttpResult(resultMapper);
        })
        .Accepts<Delta<NewOrganization>>("application/json", "application/*+json")
        .Produces<ViewOrganization>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Update")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyRequired = true,
            RequestBodyDescription = "The changes",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the organization.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the organization.",
                ["404"] = "The organization could not be found.",
            }
        });

        group.MapPost("organizations/{id:objectid}/icon", UploadIconAsync)
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<ViewOrganization>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithMetadata(
            new MultipartFileUploadAttribute(),
            new RequestSizeLimitAttribute(ProfileImageStorage.MaxRequestBodySize),
            new RequestFormLimitsAttribute { MultipartBodyLengthLimit = ProfileImageStorage.MaxRequestBodySize })
        .WithSummary("Upload icon")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the organization.",
                ["file"] = "The organization icon image file.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The organization could not be found.",
                ["422"] = "The image file is invalid.",
            }
        })
        .DisableAntiforgery();

        group.MapDelete("organizations/{id:objectid}/icon", DeleteIconAsync)
        .Produces<ViewOrganization>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Remove icon")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the organization.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The organization could not be found.",
            }
        });

        group.MapGet("organizations/{id:objectid}/icon/{fileName}", GetIconAsync)
        .AllowAnonymous()
        .WithName("GetOrganizationIcon")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get icon")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the organization.",
                ["fileName"] = "The icon file name.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The icon could not be found.",
            }
        });

        group.MapDelete("organizations/{ids:objectids}", async (string ids, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<ModelActionResults>>(new OrganizationMessages.DeleteOrganizations(ids.FromDelimitedString(), httpContext))).ToHttpResult(resultMapper))
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Remove")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["ids"] = "A comma-delimited list of organization identifiers.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["400"] = "One or more validation errors occurred.",
                ["404"] = "One or more organizations were not found.",
                ["500"] = "An error occurred while deleting one or more organizations.",
            }
        });

        group.MapGet("organizations/invoice/{id:minlength(10)}", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<Invoice>>(new OrganizationMessages.GetInvoice(id, httpContext))).ToHttpResult(resultMapper))
        .Produces<Invoice>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get invoice")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the invoice.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The invoice was not found.",
            }
        });

        group.MapGet("organizations/{id:objectid}/invoices", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, string? before = null, string? after = null, int limit = 12)
            => (await mediator.InvokeAsync<Result<PagedResult<InvoiceGridModel>>>(new OrganizationMessages.GetInvoices(id, before, after, limit, httpContext))).ToHttpResult(resultMapper))
        .Produces<IReadOnlyCollection<InvoiceGridModel>>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get invoices")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the organization.",
                ["before"] = "A cursor for use in pagination. before is an object ID that defines your place in the list. For instance, if you make a list request and receive 100 objects, starting with obj_bar, your subsequent call can include before=obj_bar in order to fetch the previous page of the list.",
                ["after"] = "A cursor for use in pagination. after is an object ID that defines your place in the list. For instance, if you make a list request and receive 100 objects, ending with obj_foo, your subsequent call can include after=obj_foo in order to fetch the next page of the list.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The organization was not found.",
            }
        });

        group.MapGet("organizations/{id:objectid}/plans", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<IReadOnlyCollection<BillingPlan>>>(new OrganizationMessages.GetPlans(id, httpContext))).ToHttpResult(resultMapper))
        .Produces<IReadOnlyCollection<BillingPlan>>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get plans")
        .WithDescription("Gets available plans for a specific organization.")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the organization.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The organization was not found.",
            }
        });

        group.MapPost("organizations/{id:objectid}/change-plan", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ChangePlanRequest? model = null,
            [FromQuery] string? planId = null,
            [FromQuery] string? stripeToken = null,
            [FromQuery] string? last4 = null,
            [FromQuery] string? couponId = null)
            => (await mediator.InvokeAsync<Result<ChangePlanResult>>(new OrganizationMessages.ChangeOrganizationPlan(id, model, planId, stripeToken, last4, couponId, httpContext))).ToHttpResult(resultMapper))
        .Accepts<ChangePlanRequest>(true, "application/json", "application/*+json", "application/octet-stream", "text/json", "text/plain")
        .Produces<ChangePlanResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Change plan")
        .WithDescription("Upgrades or downgrades the organization's plan. Accepts parameters via JSON body (preferred) or query string (legacy).")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The plan change request (JSON body).",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the organization.",
                ["planId"] = "Legacy query parameter: the plan identifier.",
                ["stripeToken"] = "Legacy query parameter: the Stripe token.",
                ["last4"] = "Legacy query parameter: last four digits of the card.",
                ["couponId"] = "Legacy query parameter: the coupon identifier.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The organization was not found.",
            }
        });

        group.MapPost("organizations/{id:objectid}/users/{email:minlength(1)}", async (string id, string email, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<User>>(new OrganizationMessages.AddOrganizationUser(id, email, httpContext))).ToHttpResult(resultMapper))
        .Produces<User>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Add user")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the organization.",
                ["email"] = "The email address of the user you wish to add to your organization.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The organization was not found.",
                ["426"] = "Please upgrade your plan to add an additional user.",
            }
        });

        group.MapDelete("organizations/{id:objectid}/users/{email:minlength(1)}", async (string id, string email, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new OrganizationMessages.RemoveOrganizationUser(id, email, httpContext))).ToHttpResult(resultMapper))
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Remove user")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the organization.",
                ["email"] = "The email address of the user you wish to remove from your organization.",
            },
            ResponseDescriptions = new() {
                ["400"] = "The error occurred while removing the user from your organization",
                ["404"] = "The organization was not found.",
            }
        });

        group.MapPost("organizations/{id:objectid}/suspend", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, SuspensionCode? code = null, string? notes = null) =>
        {
            var contentTypeResult = ApiValidation.ValidateJsonContentType(httpContext.Request);
            if (contentTypeResult is not null)
                return contentTypeResult;

            return (await mediator.InvokeAsync<Result>(new OrganizationMessages.SuspendOrganization(id, code ?? SuspensionCode.Billing, notes, httpContext))).ToHttpResult(resultMapper);
        })
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapDelete("organizations/{id:objectid}/suspend", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new OrganizationMessages.UnsuspendOrganization(id, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapPost("organizations/{id:objectid}/data/{key:minlength(1)}", async (string id, string key, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromBody] ValueFromBody<string> value)
            => (await mediator.InvokeAsync<Result>(new OrganizationMessages.SetOrganizationData(id, key, value, httpContext))).ToHttpResult(resultMapper))
        .Accepts<ValueFromBody<string>>("application/json", "application/*+json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Add custom data")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "Any string value.",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the organization.",
                ["key"] = "The key name of the data object.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The organization was not found.",
            }
        });

        group.MapDelete("organizations/{id:objectid}/data/{key:minlength(1)}", async (string id, string key, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new OrganizationMessages.DeleteOrganizationData(id, key, httpContext))).ToHttpResult(resultMapper))
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Remove custom data")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the organization.",
                ["key"] = "The key name of the data object.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The organization was not found.",
            }
        });

        group.MapPost("organizations/{id:objectid}/features/{feature:minlength(1)}", async (string id, string feature, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new OrganizationMessages.SetOrganizationFeature(id, feature, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapDelete("organizations/{id:objectid}/features/{feature:minlength(1)}", async (string id, string feature, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new OrganizationMessages.RemoveOrganizationFeature(id, feature, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapGet("organizations/check-name", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, string? name = null)
            => (await mediator.InvokeAsync<Result>(new OrganizationMessages.CheckOrganizationName(name, httpContext))).ToHttpResult(resultMapper))
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status204NoContent)
        .WithSummary("Check for unique name")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["name"] = "The organization name to check.",
            },
            ResponseDescriptions = new() {
                ["201"] = "The organization name is available.",
                ["204"] = "The organization name is not available.",
            }
        });

        return endpoints;
    }

    private static async Task<HttpIResult> UploadIconAsync(string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromServices] IFileStorage fileStorage, CancellationToken cancellationToken)
    {
        var accessResult = await mediator.InvokeAsync<Result<ViewOrganization>>(new OrganizationMessages.GetOrganizationById(id, null, httpContext));
        if (!accessResult.IsSuccess)
            return accessResult.ToHttpResult(resultMapper);

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        var modelState = new ModelStateDictionary();
        var image = await ProfileImageStorage.SaveAsync(fileStorage, file, "organizations", id, modelState, cancellationToken);
        if (image is null)
            return ValidationProblem(modelState);

        try
        {
            var result = await mediator.InvokeAsync<Result<ProfileImageUpdate<ViewOrganization>>>(new OrganizationMessages.SetOrganizationIcon(id, image.FileName, httpContext));
            if (!result.IsSuccess)
            {
                await ProfileImageStorage.TryDeleteAsync(fileStorage, image.FileName, "organizations", id, CancellationToken.None);
                return result.ToHttpResult(resultMapper);
            }

            var update = result.ValueOrDefault!;
            await ProfileImageStorage.DeleteAsync(fileStorage, update.PreviousFileName, "organizations", id, cancellationToken);
            return HttpResults.Ok(update.View);
        }
        catch
        {
            await ProfileImageStorage.TryDeleteAsync(fileStorage, image.FileName, "organizations", id, CancellationToken.None);
            throw;
        }
    }

    private static async Task<HttpIResult> DeleteIconAsync(string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromServices] IFileStorage fileStorage, CancellationToken cancellationToken)
    {
        var result = await mediator.InvokeAsync<Result<ProfileImageUpdate<ViewOrganization>>>(new OrganizationMessages.DeleteOrganizationIcon(id, httpContext));
        if (!result.IsSuccess)
            return result.ToHttpResult(resultMapper);

        var update = result.ValueOrDefault!;
        await ProfileImageStorage.DeleteAsync(fileStorage, update.PreviousFileName, "organizations", id, cancellationToken);
        return HttpResults.Ok(update.View);
    }

    private static async Task<HttpIResult> GetIconAsync(string id, string fileName, HttpContext httpContext, [FromServices] IFileStorage fileStorage, CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = ProfileImageStorage.PublicCacheControl;

        if (!ProfileImageStorage.TryGetContentType(fileName, out string contentType))
            return HttpResults.NotFound();

        var stream = await ProfileImageStorage.GetFileStreamAsync(fileStorage, fileName, "organizations", id, cancellationToken);
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
