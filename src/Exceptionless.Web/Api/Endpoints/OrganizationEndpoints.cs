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
using Foundatio.Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrganizationMessages = Exceptionless.Web.Api.Messages;
using Invoice = Exceptionless.Web.Models.Invoice;
using Exceptionless.Web.Utility.OpenApi;

namespace Exceptionless.Web.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("Organization");

        group.MapGet("organizations", async (HttpContext httpContext, IMediator mediator, string? filter = null, string? mode = null)
            => (await mediator.InvokeAsync<Result<IReadOnlyCollection<ViewOrganization>>>(new OrganizationMessages.GetOrganizations(filter, mode, httpContext))).ToHttpResult())
        .Produces<IReadOnlyCollection<ViewOrganization>>()
        .WithSummary("Get all")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["mode"] = "If no mode is set then a lightweight organization object will be returned. If the mode is set to stats than the fully populated object will be returned.",
            }
        });

        group.MapGet("admin/organizations", async (HttpContext httpContext, IMediator mediator, string? criteria = null, bool? paid = null, bool? suspended = null, string? mode = null, int page = 1, int limit = 10, OrganizationSortBy sort = OrganizationSortBy.Newest)
            => (await mediator.InvokeAsync<Result<PagedResult<ViewOrganization>>>(new OrganizationMessages.GetAdminOrganizations(criteria, paid, suspended, mode, page, limit, sort, httpContext))).ToHttpResult())
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces<IReadOnlyCollection<ViewOrganization>>()
        .ExcludeFromDescription();

        group.MapGet("admin/organizations/stats", async (HttpContext httpContext, IMediator mediator)
            => (await mediator.InvokeAsync<Result<BillingPlanStats>>(new OrganizationMessages.GetOrganizationPlanStats(httpContext))).ToHttpResult())
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces<BillingPlanStats>()
        .ExcludeFromDescription();

        group.MapGet("organizations/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, string? mode = null)
            => (await mediator.InvokeAsync<Result<ViewOrganization>>(new OrganizationMessages.GetOrganizationById(id, mode, httpContext))).ToHttpResult())
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

        group.MapPost("organizations", async (HttpContext httpContext, IMediator mediator, IServiceProvider serviceProvider, [FromBody] NewOrganization organization) =>
        {
            var validation = await ApiValidation.ValidateAsync(organization, serviceProvider);
            if (validation is not null)
                return validation;

            return (await mediator.InvokeAsync<Result<ViewOrganization>>(new OrganizationMessages.CreateOrganization(organization, httpContext))).ToHttpResult();
        })
        .Accepts<NewOrganization>("application/json")
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

        group.MapPatch("organizations/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, [FromBody] Delta<NewOrganization> changes)
            => (await mediator.InvokeAsync<Result<ViewOrganization>>(new OrganizationMessages.UpdateOrganizationMessage(id, changes, httpContext))).ToHttpResult())
        .Accepts<Delta<NewOrganization>>("application/json")
        .Produces<ViewOrganization>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Update")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The changes",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the organization.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the organization.",
                ["404"] = "The organization could not be found.",
            }
        });

        group.MapPut("organizations/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, [FromBody] Delta<NewOrganization> changes)
            => (await mediator.InvokeAsync<Result<ViewOrganization>>(new OrganizationMessages.UpdateOrganizationMessage(id, changes, httpContext))).ToHttpResult())
        .Accepts<Delta<NewOrganization>>("application/json")
        .Produces<ViewOrganization>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Update")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The changes",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the organization.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the organization.",
                ["404"] = "The organization could not be found.",
            }
        });

        group.MapDelete("organizations/{ids:objectids}", async (string ids, HttpContext httpContext, IMediator mediator)
            => (await mediator.InvokeAsync<Result<ModelActionResults>>(new OrganizationMessages.DeleteOrganizations(ids.FromDelimitedString(), httpContext))).ToHttpResult())
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

        group.MapGet("organizations/invoice/{id:minlength(10)}", async (string id, HttpContext httpContext, IMediator mediator)
            => (await mediator.InvokeAsync<Result<Invoice>>(new OrganizationMessages.GetInvoice(id, httpContext))).ToHttpResult())
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

        group.MapGet("organizations/{id:objectid}/invoices", async (string id, HttpContext httpContext, IMediator mediator, string? before = null, string? after = null, int limit = 12)
            => (await mediator.InvokeAsync<Result<PagedResult<InvoiceGridModel>>>(new OrganizationMessages.GetInvoices(id, before, after, limit, httpContext))).ToHttpResult())
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

        group.MapGet("organizations/{id:objectid}/plans", async (string id, HttpContext httpContext, IMediator mediator)
            => (await mediator.InvokeAsync<Result<IReadOnlyCollection<BillingPlan>>>(new OrganizationMessages.GetPlans(id, httpContext))).ToHttpResult())
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

        group.MapPost("organizations/{id:objectid}/change-plan", async (string id, HttpContext httpContext, IMediator mediator,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ChangePlanRequest? model = null,
            [FromQuery] string? planId = null,
            [FromQuery] string? stripeToken = null,
            [FromQuery] string? last4 = null,
            [FromQuery] string? couponId = null)
            => (await mediator.InvokeAsync<Result<ChangePlanResult>>(new OrganizationMessages.ChangeOrganizationPlan(id, model, planId, stripeToken, last4, couponId, httpContext))).ToHttpResult())
        .Accepts<ChangePlanRequest>("application/json")
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

        group.MapPost("organizations/{id:objectid}/users/{email:minlength(1)}", async (string id, string email, HttpContext httpContext, IMediator mediator)
            => (await mediator.InvokeAsync<Result<User>>(new OrganizationMessages.AddOrganizationUser(id, email, httpContext))).ToHttpResult())
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

        group.MapDelete("organizations/{id:objectid}/users/{email:minlength(1)}", async (string id, string email, HttpContext httpContext, IMediator mediator)
            => (await mediator.InvokeAsync<Result>(new OrganizationMessages.RemoveOrganizationUser(id, email, httpContext))).ToHttpResult())
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

        group.MapPost("organizations/{id:objectid}/suspend", async (string id, HttpContext httpContext, IMediator mediator, SuspensionCode? code = null, string? notes = null)
            => (await mediator.InvokeAsync<Result>(new OrganizationMessages.SuspendOrganization(id, code ?? SuspensionCode.Billing, notes, httpContext))).ToHttpResult())
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapDelete("organizations/{id:objectid}/suspend", async (string id, HttpContext httpContext, IMediator mediator)
            => (await mediator.InvokeAsync<Result>(new OrganizationMessages.UnsuspendOrganization(id, httpContext))).ToHttpResult())
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapPost("organizations/{id:objectid}/data/{key:minlength(1)}", async (string id, string key, HttpContext httpContext, IMediator mediator, [FromBody] ValueFromBody<string> value)
            => (await mediator.InvokeAsync<Result>(new OrganizationMessages.SetOrganizationData(id, key, value, httpContext))).ToHttpResult())
        .Accepts<ValueFromBody<string>>("application/json")
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

        group.MapDelete("organizations/{id:objectid}/data/{key:minlength(1)}", async (string id, string key, HttpContext httpContext, IMediator mediator)
            => (await mediator.InvokeAsync<Result>(new OrganizationMessages.DeleteOrganizationData(id, key, httpContext))).ToHttpResult())
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

        group.MapPost("organizations/{id:objectid}/features/{feature:minlength(1)}", async (string id, string feature, HttpContext httpContext, IMediator mediator)
            => (await mediator.InvokeAsync<Result>(new OrganizationMessages.SetOrganizationFeature(id, feature, httpContext))).ToHttpResult())
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapDelete("organizations/{id:objectid}/features/{feature:minlength(1)}", async (string id, string feature, HttpContext httpContext, IMediator mediator)
            => (await mediator.InvokeAsync<Result>(new OrganizationMessages.RemoveOrganizationFeature(id, feature, httpContext))).ToHttpResult())
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapGet("organizations/check-name", async (string name, HttpContext httpContext, IMediator mediator)
            => (await mediator.InvokeAsync<Result>(new OrganizationMessages.CheckOrganizationName(name, httpContext))).ToHttpResult())
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
}
