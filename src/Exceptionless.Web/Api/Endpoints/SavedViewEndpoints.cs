using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Seed;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using IMediator = Foundatio.Mediator.IMediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SavedViewMessages = Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Utility.OpenApi;

namespace Exceptionless.Web.Api.Endpoints;

public static class SavedViewEndpoints
{
    public static IEndpointRouteBuilder MapSavedViewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("SavedView");

        group.MapGet("organizations/{organizationId:objectid}/saved-views", async (string organizationId, IMediator mediator, int page = 1, int limit = 25)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.GetSavedViewsByOrganization(organizationId, page, limit)))
        .Produces<IReadOnlyCollection<ViewSavedView>>()
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

        group.MapGet("organizations/{organizationId:objectid}/saved-views/{viewType}", async (string organizationId, string viewType, IMediator mediator, int page = 1, int limit = 25)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.GetSavedViewsByView(organizationId, viewType, page, limit)))
        .Produces<IReadOnlyCollection<ViewSavedView>>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get by organization and view")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["organizationId"] = "The identifier of the organization.",
                ["viewType"] = "The dashboard view type (events, issues, stream).",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The organization could not be found.",
            }
        });

        group.MapGet("saved-views/{id:objectid}", async (string id, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.GetSavedViewById(id)))
        .WithName("GetSavedViewById")
        .Produces<ViewSavedView>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get by id")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the saved view.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The saved view could not be found.",
            }
        });

        group.MapPost("organizations/{organizationId:objectid}/saved-views", async (string organizationId, IMediator mediator, IServiceProvider serviceProvider,
            [FromBody] NewSavedView savedView) =>
        {
            var validation = await ApiValidation.ValidateAsync(savedView, serviceProvider);
            if (validation is not null)
                return validation;

            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.CreateSavedView(organizationId, savedView));
        })
        .Produces<ViewSavedView>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Create")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["organizationId"] = "The identifier of the organization.",
            },
            ResponseDescriptions = new() {
                ["201"] = "Created",
                ["400"] = "An error occurred while creating the saved view.",
                ["409"] = "The saved view already exists.",
            }
        });

        group.MapPost("organizations/{organizationId:objectid}/saved-views/predefined", async (string organizationId, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.CreatePredefinedSavedViews(organizationId)))
        .Produces<IReadOnlyCollection<ViewSavedView>>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Create or update predefined saved views")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["organizationId"] = "The identifier of the organization.",
            },
            ResponseDescriptions = new() {
                ["200"] = "The predefined saved views were created or updated.",
                ["404"] = "The organization could not be found.",
            }
        });

        group.MapGet("saved-views/predefined", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.GetPredefinedSavedViews()))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces<IReadOnlyCollection<PredefinedSavedViewDefinition>>()
        .WithSummary("Get global predefined saved views as seed JSON")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["200"] = "The current predefined saved views.",
            }
        });

        group.MapPost("saved-views/{id:objectid}/predefined", async (string id, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.PromoteToPredefinedSavedView(id)))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces<ViewSavedView>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Save a saved view as a global predefined saved view")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the saved view to promote.",
            },
            ResponseDescriptions = new() {
                ["200"] = "The predefined saved view was created or updated.",
                ["404"] = "The saved view could not be found.",
            }
        });

        group.MapDelete("saved-views/{id:objectid}/predefined", async (string id, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.DeletePredefinedSavedView(id)))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete a global predefined saved view")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the saved view whose predefined saved view should be deleted.",
            },
            ResponseDescriptions = new() {
                ["204"] = "The predefined saved view was deleted.",
                ["404"] = "The saved view could not be found.",
            }
        });

        group.MapPatch("saved-views/{id:objectid}", async (string id, IMediator mediator, [FromBody] Delta<UpdateSavedView> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.UpdateSavedViewMessage(id, changes)))
        .Produces<ViewSavedView>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Update")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the saved view.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the saved view.",
                ["404"] = "The saved view could not be found.",
            }
        });

        group.MapPut("saved-views/{id:objectid}", async (string id, IMediator mediator, [FromBody] Delta<UpdateSavedView> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.UpdateSavedViewMessage(id, changes)))
        .Produces<ViewSavedView>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Update")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the saved view.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the saved view.",
                ["404"] = "The saved view could not be found.",
            }
        });

        group.MapDelete("saved-views/{ids:objectids}", async (string ids, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.DeleteSavedViews(ids.FromDelimitedString())))
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Remove")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["ids"] = "A comma-delimited list of saved view identifiers.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["400"] = "One or more validation errors occurred.",
                ["404"] = "One or more saved views were not found.",
                ["500"] = "An error occurred while deleting one or more saved views.",
            }
        });

        return endpoints;
    }
}
