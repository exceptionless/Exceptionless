using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using IMediator = Foundatio.Mediator.IMediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrganizationMessages = Exceptionless.Web.Api.Messages;
using Invoice = Exceptionless.Web.Models.Invoice;

namespace Exceptionless.Web.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .WithTags("Organizations");

        group.MapGet("organizations", async (HttpContext httpContext, IMediator mediator, string? filter = null, string? mode = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.GetOrganizations(filter, mode, httpContext)))
        .Produces<IReadOnlyCollection<ViewOrganization>>();

        group.MapGet("admin/organizations", async (HttpContext httpContext, IMediator mediator, string? criteria = null, bool? paid = null, bool? suspended = null, string? mode = null, int page = 1, int limit = 10, OrganizationSortBy sort = OrganizationSortBy.Newest)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.GetAdminOrganizations(criteria, paid, suspended, mode, page, limit, sort, httpContext)))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces<IReadOnlyCollection<ViewOrganization>>()
        .ExcludeFromDescription();

        group.MapGet("admin/organizations/stats", async (HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.GetOrganizationPlanStats(httpContext)))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces<BillingPlanStats>()
        .ExcludeFromDescription();

        group.MapGet("organizations/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, string? mode = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.GetOrganizationById(id, mode, httpContext)))
        .WithName("GetOrganizationById")
        .Produces<ViewOrganization>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("organizations", async (HttpContext httpContext, IMediator mediator, IServiceProvider serviceProvider, [FromBody] NewOrganization organization) =>
        {
            var validation = await ApiValidation.ValidateAsync(organization, serviceProvider);
            if (validation is not null)
                return validation;

            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.CreateOrganization(organization, httpContext));
        })
        .Accepts<NewOrganization>("application/json")
        .Produces<ViewOrganization>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPatch("organizations/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, [FromBody] Delta<NewOrganization> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.UpdateOrganizationMessage(id, changes, httpContext)))
        .Accepts<Delta<NewOrganization>>("application/json")
        .Produces<ViewOrganization>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("organizations/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, [FromBody] Delta<NewOrganization> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.UpdateOrganizationMessage(id, changes, httpContext)))
        .Accepts<Delta<NewOrganization>>("application/json")
        .Produces<ViewOrganization>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("organizations/{ids:objectids}", async (string ids, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.DeleteOrganizations(ids.FromDelimitedString(), httpContext)))
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("organizations/invoice/{id:minlength(10)}", async (string id, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.GetInvoice(id, httpContext)))
        .Produces<Invoice>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("organizations/{id:objectid}/invoices", async (string id, HttpContext httpContext, IMediator mediator, string? before = null, string? after = null, int limit = 12)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.GetInvoices(id, before, after, limit, httpContext)))
        .Produces<IReadOnlyCollection<InvoiceGridModel>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("organizations/{id:objectid}/plans", async (string id, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.GetPlans(id, httpContext)))
        .Produces<IReadOnlyCollection<BillingPlan>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("organizations/{id:objectid}/change-plan", async (string id, HttpContext httpContext, IMediator mediator,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ChangePlanRequest? model = null,
            [FromQuery] string? planId = null,
            [FromQuery] string? stripeToken = null,
            [FromQuery] string? last4 = null,
            [FromQuery] string? couponId = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.ChangeOrganizationPlan(id, model, planId, stripeToken, last4, couponId, httpContext)))
        .Accepts<ChangePlanRequest>("application/json")
        .Produces<ChangePlanResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("organizations/{id:objectid}/users/{email:minlength(1)}", async (string id, string email, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.AddOrganizationUser(id, email, httpContext)))
        .Produces<User>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired);

        group.MapDelete("organizations/{id:objectid}/users/{email:minlength(1)}", async (string id, string email, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.RemoveOrganizationUser(id, email, httpContext)))
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("organizations/{id:objectid}/suspend", async (string id, SuspensionCode code, HttpContext httpContext, IMediator mediator, string? notes = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.SuspendOrganization(id, code, notes, httpContext)))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapDelete("organizations/{id:objectid}/suspend", async (string id, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.UnsuspendOrganization(id, httpContext)))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapPost("organizations/{id:objectid}/data/{key:minlength(1)}", async (string id, string key, HttpContext httpContext, IMediator mediator, [FromBody] ValueFromBody<string> value)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.SetOrganizationData(id, key, value, httpContext)))
        .Accepts<ValueFromBody<string>>("application/json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("organizations/{id:objectid}/data/{key:minlength(1)}", async (string id, string key, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.DeleteOrganizationData(id, key, httpContext)))
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("organizations/{id:objectid}/features/{feature:minlength(1)}", async (string id, string feature, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.SetOrganizationFeature(id, feature, httpContext)))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapDelete("organizations/{id:objectid}/features/{feature:minlength(1)}", async (string id, string feature, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.RemoveOrganizationFeature(id, feature, httpContext)))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapGet("organizations/check-name", async (string name, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new OrganizationMessages.CheckOrganizationName(name, httpContext)))
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }
}
