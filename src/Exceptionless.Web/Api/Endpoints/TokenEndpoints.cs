using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TokenMessages = Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Utility.OpenApi;

namespace Exceptionless.Web.Api.Endpoints;

public static class TokenEndpoints
{
    public static IEndpointRouteBuilder MapTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("Token");

        group.MapGet("organizations/{organizationId:objectid}/tokens", async (string organizationId, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, int page = 1, int limit = 10)
            => (await mediator.InvokeAsync<Result<PagedResult<ViewToken>>>(new TokenMessages.GetTokensByOrganization(organizationId, page, limit))).ToHttpResult(resultMapper))
        .Produces(StatusCodes.Status200OK)
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

        group.MapGet("projects/{projectId:objectid}/tokens", async (string projectId, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, int page = 1, int limit = 10)
            => (await mediator.InvokeAsync<Result<PagedResult<ViewToken>>>(new TokenMessages.GetTokensByProject(projectId, page, limit))).ToHttpResult(resultMapper))
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

        group.MapGet("projects/{projectId:objectid}/tokens/default", async (string projectId, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<ViewToken>>(new TokenMessages.GetDefaultToken(projectId))).ToHttpResult(resultMapper))
        .Produces<ViewToken>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get a projects default token")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["projectId"] = "The identifier of the project.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The project could not be found.",
            }
        });

        group.MapGet("tokens/{id:token}", async (string id, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<ViewToken>>(new TokenMessages.GetTokenById(id))).ToHttpResult(resultMapper))
        .WithName("GetTokenById")
        .Produces<ViewToken>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get by id")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the token.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The token could not be found.",
            }
        });

        group.MapPost("tokens", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, IServiceProvider serviceProvider, [FromBody] NewToken token) =>
        {
            var validation = await ApiValidation.ValidateAsync(token, serviceProvider);
            if (validation is not null)
                return validation;

            if (String.IsNullOrEmpty(token.ProjectId))
                return Microsoft.AspNetCore.Http.Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["project_id"] = ["The project_id field is required."] },
                    statusCode: StatusCodes.Status400BadRequest);

            return (await mediator.InvokeAsync<Result<ViewToken>>(new TokenMessages.CreateToken(token))).ToHttpResult(resultMapper);
        })
        .Produces<ViewToken>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithSummary("Create")
        .WithDescription("To create a new token, you must specify an organization_id. There are three valid scopes: client, user and admin.")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The token.",
            ResponseDescriptions = new() {
                ["201"] = "Created",
                ["400"] = "An error occurred while creating the token.",
                ["409"] = "The token already exists.",
            }
        });

        group.MapPost("projects/{projectId:objectid}/tokens", async (string projectId, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, IServiceProvider serviceProvider,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NewToken? token = null) =>
        {
            if (token is not null)
            {
                var validation = await ApiValidation.ValidateAsync(token, serviceProvider);
                if (validation is not null)
                    return validation;
            }

            return (await mediator.InvokeAsync<Result<ViewToken>>(new TokenMessages.CreateTokenByProject(projectId, token))).ToHttpResult(resultMapper);
        })
        .Produces<ViewToken>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithSummary("Create for project")
        .WithDescription("This is a helper action that makes it easier to create a token for a specific project. You may also specify a scope when creating a token. There are three valid scopes: client, user and admin.")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The token.",
            ParameterDescriptions = new() {
                ["projectId"] = "The identifier of the project.",
            },
            ResponseDescriptions = new() {
                ["201"] = "Created",
                ["400"] = "An error occurred while creating the token.",
                ["404"] = "The project could not be found.",
                ["409"] = "The token already exists.",
            }
        });

        group.MapPost("organizations/{organizationId:objectid}/tokens", async (string organizationId, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, IServiceProvider serviceProvider,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NewToken? token = null) =>
        {
            if (token is not null)
            {
                var validation = await ApiValidation.ValidateAsync(token, serviceProvider);
                if (validation is not null)
                    return validation;
            }

            return (await mediator.InvokeAsync<Result<ViewToken>>(new TokenMessages.CreateTokenByOrganization(organizationId, token))).ToHttpResult(resultMapper);
        })
        .Produces<ViewToken>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithSummary("Create for organization")
        .WithDescription("This is a helper action that makes it easier to create a token for a specific organization. You may also specify a scope when creating a token. There are three valid scopes: client, user and admin.")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The token.",
            ParameterDescriptions = new() {
                ["organizationId"] = "The identifier of the organization.",
            },
            ResponseDescriptions = new() {
                ["201"] = "Created",
                ["400"] = "An error occurred while creating the token.",
                ["409"] = "The token already exists.",
            }
        });

        group.MapPatch("tokens/{id:tokens}", async (string id, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, [FromBody] Delta<UpdateToken>? changes)
            => changes is null ? ApiValidation.MissingRequestBody() : (await mediator.InvokeAsync<Result<ViewToken>>(new TokenMessages.UpdateTokenMessage(id, changes))).ToHttpResult(resultMapper))
        .Accepts<Delta<UpdateToken>>(false, "application/json")
        .Produces<ViewToken>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The changes",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the token.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the token.",
                ["404"] = "The token could not be found.",
            }
        });

        group.MapPut("tokens/{id:tokens}", async (string id, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, [FromBody] Delta<UpdateToken>? changes)
            => changes is null ? ApiValidation.MissingRequestBody() : (await mediator.InvokeAsync<Result<ViewToken>>(new TokenMessages.UpdateTokenMessage(id, changes))).ToHttpResult(resultMapper))
        .Accepts<Delta<UpdateToken>>(false, "application/json")
        .Produces<ViewToken>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The changes",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the token.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the token.",
                ["404"] = "The token could not be found.",
            }
        });

        group.MapDelete("tokens/{ids:tokens}", async (string ids, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<ModelActionResults>>(new TokenMessages.DeleteTokens(ids.FromDelimitedString()))).ToHttpResult(resultMapper))
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Remove")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["ids"] = "A comma-delimited list of token identifiers.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["400"] = "One or more validation errors occurred.",
                ["404"] = "One or more tokens were not found.",
                ["500"] = "An error occurred while deleting one or more tokens.",
            }
        });

        return endpoints;
    }
}
