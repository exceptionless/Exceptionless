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

namespace Exceptionless.Web.Api.Endpoints;

public static class SavedViewEndpoints
{
    public static IEndpointRouteBuilder MapSavedViewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("Saved Views");

        group.MapGet("organizations/{organizationId:objectid}/saved-views", async (string organizationId, IMediator mediator, int page = 1, int limit = 25)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.GetSavedViewsByOrganization(organizationId, page, limit)))
        .Produces<IReadOnlyCollection<ViewSavedView>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("organizations/{organizationId:objectid}/saved-views/{viewType}", async (string organizationId, string viewType, IMediator mediator, int page = 1, int limit = 25)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.GetSavedViewsByView(organizationId, viewType, page, limit)))
        .Produces<IReadOnlyCollection<ViewSavedView>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("saved-views/{id:objectid}", async (string id, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.GetSavedViewById(id)))
        .WithName("GetSavedViewById")
        .Produces<ViewSavedView>()
        .ProducesProblem(StatusCodes.Status404NotFound);

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
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("organizations/{organizationId:objectid}/saved-views/predefined", async (string organizationId, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.CreatePredefinedSavedViews(organizationId)))
        .Produces<IReadOnlyCollection<ViewSavedView>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("saved-views/predefined", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.GetPredefinedSavedViews()))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces<IReadOnlyCollection<PredefinedSavedViewDefinition>>();

        group.MapPost("saved-views/{id:objectid}/predefined", async (string id, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.PromoteToPredefinedSavedView(id)))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces<ViewSavedView>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("saved-views/{id:objectid}/predefined", async (string id, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.DeletePredefinedSavedView(id)))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("saved-views/{id:objectid}", async (string id, IMediator mediator, [FromBody] Delta<UpdateSavedView> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.UpdateSavedViewMessage(id, changes)))
        .Produces<ViewSavedView>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("saved-views/{id:objectid}", async (string id, IMediator mediator, [FromBody] Delta<UpdateSavedView> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.UpdateSavedViewMessage(id, changes)))
        .Produces<ViewSavedView>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("saved-views/{ids:objectids}", async (string ids, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SavedViewMessages.DeleteSavedViews(ids.FromDelimitedString())))
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
