using System.Text.Json;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Mvc;
using WebHookMessages = Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Utility.OpenApi;

namespace Exceptionless.Web.Api.Endpoints;

public static class WebHookEndpoints
{
    public static IEndpointRouteBuilder MapWebHookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.ClientPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("WebHook");

        group.MapGet("projects/{projectId:objectid}/webhooks", async (string projectId, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, int page = 1, int limit = 10)
            => (await mediator.InvokeAsync<Result<PagedResult<WebHook>>>(new WebHookMessages.GetWebHooksByProject(projectId, page, limit))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get by project")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["projectId"] = "The identifier of the project.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The project could not be found.",
            }
        });

        group.MapGet("webhooks/{id:objectid}", async (string id, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<WebHook>>(new WebHookMessages.GetWebHookById(id))).ToHttpResult(resultMapper))
        .WithName("GetWebHookById")
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<WebHook>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get by id")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the web hook.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The web hook could not be found.",
            }
        });

        group.MapPost("webhooks", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, IServiceProvider serviceProvider, [FromBody] NewWebHook webHook) =>
        {
            var validation = await ApiValidation.ValidateAsync(webHook, serviceProvider);
            if (validation is not null)
                return validation;

            return (await mediator.InvokeAsync<Result<WebHook>>(new WebHookMessages.CreateWebHook(webHook))).ToHttpResult(resultMapper);
        })
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<WebHook>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithSummary("Create")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The web hook.",
            ResponseDescriptions = new() {
                ["201"] = "Created",
                ["400"] = "An error occurred while creating the web hook.",
                ["409"] = "The web hook already exists.",
            }
        });

        group.MapDelete("webhooks/{ids:objectids}", async (string ids, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<ModelActionResults>>(new WebHookMessages.DeleteWebHooks(ids.FromDelimitedString()))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Remove")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["ids"] = "A comma-delimited list of web hook identifiers.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["400"] = "One or more validation errors occurred.",
                ["404"] = "One or more web hooks were not found.",
                ["500"] = "An error occurred while deleting one or more web hooks.",
            }
        });

        group.MapPost("webhooks/subscribe", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, [FromBody] JsonDocument data)
            => (await mediator.InvokeAsync<Result<WebHook>>(new WebHookMessages.SubscribeWebHook(data, 1))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .ExcludeFromDescription();

        endpoints.MapPost("api/v{apiVersion:int=2}/webhooks/subscribe", async (int apiVersion, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, [FromBody] JsonDocument data)
            => (await mediator.InvokeAsync<Result<WebHook>>(new WebHookMessages.SubscribeWebHook(data, apiVersion))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .ExcludeFromDescription();

        group.MapPost("webhooks/unsubscribe", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, [FromBody] JsonDocument data)
            => (await mediator.InvokeAsync<Result>(new WebHookMessages.UnsubscribeWebHook(data))).ToHttpResult(resultMapper))
        .AllowAnonymous()
        .ExcludeFromDescription();

        group.MapGet("webhooks/test", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<object[]>>(new WebHookMessages.TestWebHook())).ToHttpResult(resultMapper))
        .ExcludeFromDescription();

        group.MapPost("webhooks/test", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<object[]>>(new WebHookMessages.TestWebHook())).ToHttpResult(resultMapper))
        .ExcludeFromDescription();

        endpoints.MapPost("api/v1/projecthook/subscribe", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, [FromBody] JsonDocument data)
            => (await mediator.InvokeAsync<Result<WebHook>>(new WebHookMessages.SubscribeWebHook(data, 1))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .ExcludeFromDescription();

        endpoints.MapPost("api/v1/projecthook/unsubscribe", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, [FromBody] JsonDocument data)
            => (await mediator.InvokeAsync<Result>(new WebHookMessages.UnsubscribeWebHook(data))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AllowAnonymous()
        .ExcludeFromDescription();

        endpoints.MapGet("api/v1/projecthook/test", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<object[]>>(new WebHookMessages.TestWebHook())).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .ExcludeFromDescription();

        endpoints.MapPost("api/v1/projecthook/test", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<object[]>>(new WebHookMessages.TestWebHook())).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .ExcludeFromDescription();

        return endpoints;
    }
}
