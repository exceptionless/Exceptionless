using System.Text.Json;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Extensions;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using Foundatio.Mediator;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using ProjectMessages = Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Utility.OpenApi;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using HttpIResult = Microsoft.AspNetCore.Http.IResult;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Exceptionless.Web.Api.Endpoints;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("Project");

        group.MapGet("projects", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, string? filter = null, string? sort = null, int page = 1, int limit = 10, string? mode = null)
            => (await mediator.InvokeAsync<Result<PagedResult<ViewProject>>>(new ProjectMessages.GetProjects(filter, sort, page, limit, mode, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.ProjectsReadPolicy)
        .Produces<IReadOnlyCollection<ViewProject>>()
        .WithSummary("Get all")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["sort"] = "Controls the sort order that the data is returned in. In this example -created returns the results descending by the created date.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
                ["mode"] = "If no mode is set then the lightweight project object will be returned. If the mode is set to stats than the fully populated object will be returned.",
            }
        });

        group.MapGet("organizations/{organizationId:objectid}/projects", async (string organizationId, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, string? filter = null, string? sort = null, int page = 1, int limit = 10, string? mode = null)
            => (await mediator.InvokeAsync<Result<PagedResult<ViewProject>>>(new ProjectMessages.GetProjectsByOrganization(organizationId, filter, sort, page, limit, mode, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.ProjectsReadPolicy)
        .Produces<IReadOnlyCollection<ViewProject>>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get all")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["organizationId"] = "The identifier of the organization.",
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["sort"] = "Controls the sort order that the data is returned in. In this example -created returns the results descending by the created date.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
                ["mode"] = "If no mode is set then the lightweight project object will be returned. If the mode is set to stats than the fully populated object will be returned.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The organization could not be found.",
            }
        });

        group.MapGet("projects/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, string? mode = null)
            => (await mediator.InvokeAsync<Result<ViewProject>>(new ProjectMessages.GetProjectById(id, mode, httpContext))).ToHttpResult(resultMapper))
        .WithName("GetProjectById")
        .RequireAuthorization(AuthorizationRoles.ProjectsReadPolicy)
        .Produces<ViewProject>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get by id")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["mode"] = "If no mode is set then the lightweight project object will be returned. If the mode is set to stats than the fully populated object will be returned.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The project could not be found.",
            }
        });

        group.MapPost("projects", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, IServiceProvider serviceProvider, [FromBody] NewProject project) =>
        {
            var validation = await ApiValidation.ValidateAsync(project, serviceProvider);
            if (validation is not null)
                return validation;

            return (await mediator.InvokeAsync<Result<ViewProject>>(new ProjectMessages.CreateProject(project, httpContext))).ToHttpResult(resultMapper);
        })
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<NewProject>("application/json")
        .Produces<ViewProject>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Create")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The project.",
            ResponseDescriptions = new() {
                ["201"] = "Created",
                ["400"] = "An error occurred while creating the project.",
                ["409"] = "The project already exists.",
            }
        });

        group.MapPatch("projects/{id:objectid}", UpdateProjectAsync)
        .AcceptAnyJsonContentType()
        .WithDisplayName("HTTP: PATCH api/v2/projects/{id:objectid}")
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<ViewProject>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Update")
        .WithMetadata(new JsonPatchRequestBodyAttribute<UpdateProject>())
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The changes",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the project.",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapPut("projects/{id:objectid}", UpdateProjectAsync)
        .AcceptAnyJsonContentType()
        .WithDisplayName("HTTP: PUT api/v2/projects/{id:objectid}")
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<ViewProject>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Update")
        .WithMetadata(new JsonPatchRequestBodyAttribute<UpdateProject>())
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The changes",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the project.",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapDelete("projects/{ids:objectids}", async (string ids, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<ModelActionResults>>(new ProjectMessages.DeleteProjects(ids.FromDelimitedString(), httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Remove")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["ids"] = "A comma-delimited list of project identifiers.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["400"] = "One or more validation errors occurred.",
                ["404"] = "One or more projects were not found.",
                ["500"] = "An error occurred while deleting one or more projects.",
            }
        });

        endpoints.MapGet("api/v1/project/config", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, int? v = null)
            => (await mediator.InvokeAsync<Result<object>>(new ProjectMessages.GetLegacyProjectConfig(v, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .WithTags("Project")
        .Produces<ClientConfiguration>()
        .Produces(StatusCodes.Status304NotModified)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("projects/config", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, int? v = null)
            => (await mediator.InvokeAsync<Result<object>>(new ProjectMessages.GetProjectConfig(null, v, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .Produces<ClientConfiguration>()
        .Produces(StatusCodes.Status304NotModified)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get configuration settings")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["v"] = "The client configuration version.",
            },
            ResponseDescriptions = new() {
                ["304"] = "The client configuration version is the current version.",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapGet("projects/{id:objectid}/config", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, int? v = null)
            => (await mediator.InvokeAsync<Result<object>>(new ProjectMessages.GetProjectConfig(id, v, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .Produces<ClientConfiguration>()
        .Produces(StatusCodes.Status304NotModified)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get configuration settings")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["v"] = "The client configuration version.",
            },
            ResponseDescriptions = new() {
                ["304"] = "The client configuration version is the current version.",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapPost("projects/{id:objectid}/config", async (string id, string key, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, [FromBody] ValueFromBody<string> value)
            => (await mediator.InvokeAsync<Result>(new ProjectMessages.SetProjectConfig(id, key, value, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<ValueFromBody<string>>("application/json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Add configuration value")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The configuration value.",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["key"] = "The key name of the configuration object.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid configuration value.",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapDelete("projects/{id:objectid}/config", async (string id, string key, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new ProjectMessages.DeleteProjectConfig(id, key, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Remove configuration value")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["key"] = "The key name of the configuration object.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid key value.",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapPost("projects/{id:objectid}/sample-data", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<WorkInProgressResult>>(new ProjectMessages.GenerateProjectSampleData(id, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Generate sample project data")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapGet("projects/{id:objectid}/reset-data", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<WorkInProgressResult>>(new ProjectMessages.ResetProjectData(id, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Reset project data")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapPost("projects/{id:objectid}/reset-data", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<WorkInProgressResult>>(new ProjectMessages.ResetProjectData(id, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Reset project data")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapGet("projects/{id:objectid}/notifications", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<IDictionary<string, NotificationSettings>>>(new ProjectMessages.GetProjectNotificationSettings(id, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces<IDictionary<string, NotificationSettings>>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapGet("users/{userId:objectid}/projects/{id:objectid}/notifications", async (string id, string userId, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<NotificationSettings>>(new ProjectMessages.GetProjectUserNotificationSettings(id, userId, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<NotificationSettings>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get user notification settings")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["userId"] = "The identifier of the user.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The project could not be found.",
            }
        });

        group.MapGet("projects/{id:objectid}/{integration:minlength(1)}/notifications", async (string id, string integration, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<NotificationSettings>>(new ProjectMessages.GetProjectIntegrationNotificationSettings(id, integration, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<NotificationSettings>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapPut("users/{userId:objectid}/projects/{id:objectid}/notifications", async (string id, string userId, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NotificationSettings? settings = null)
            => (await mediator.InvokeAsync<Result>(new ProjectMessages.SetProjectUserNotificationSettings(id, userId, settings, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<NotificationSettings>("application/json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Set user notification settings")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The notification settings.",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["userId"] = "The identifier of the user.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The project could not be found.",
            }
        });

        group.MapPost("users/{userId:objectid}/projects/{id:objectid}/notifications", async (string id, string userId, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NotificationSettings? settings = null)
            => (await mediator.InvokeAsync<Result>(new ProjectMessages.SetProjectUserNotificationSettings(id, userId, settings, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<NotificationSettings>("application/json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Set user notification settings")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The notification settings.",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["userId"] = "The identifier of the user.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The project could not be found.",
            }
        });

        group.MapPut("projects/{id:objectid}/{integration:minlength(1)}/notifications", async (string id, string integration, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NotificationSettings? settings = null)
            => (await mediator.InvokeAsync<Result>(new ProjectMessages.SetProjectIntegrationNotificationSettings(id, integration, settings, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<NotificationSettings>("application/json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Set an integrations notification settings")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The notification settings.",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["integration"] = "The identifier of the integration.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The project or integration could not be found.",
                ["426"] = "Please upgrade your plan to enable integrations.",
            }
        });

        group.MapPost("projects/{id:objectid}/{integration:minlength(1)}/notifications", async (string id, string integration, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NotificationSettings? settings = null)
            => (await mediator.InvokeAsync<Result>(new ProjectMessages.SetProjectIntegrationNotificationSettings(id, integration, settings, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<NotificationSettings>("application/json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Set an integrations notification settings")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The notification settings.",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["integration"] = "The identifier of the integration.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The project or integration could not be found.",
                ["426"] = "Please upgrade your plan to enable integrations.",
            }
        });

        group.MapDelete("users/{userId:objectid}/projects/{id:objectid}/notifications", async (string id, string userId, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new ProjectMessages.DeleteProjectNotificationSettings(id, userId, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Remove user notification settings")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["userId"] = "The identifier of the user.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The project could not be found.",
            }
        });

        group.MapPut("projects/{id:objectid}/promotedtabs", async (string id, string name, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new ProjectMessages.PromoteProjectTab(id, name, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Promote tab")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["name"] = "The tab name.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid tab name.",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapPost("projects/{id:objectid}/promotedtabs", async (string id, string name, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new ProjectMessages.PromoteProjectTab(id, name, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Promote tab")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["name"] = "The tab name.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid tab name.",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapDelete("projects/{id:objectid}/promotedtabs", async (string id, string name, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new ProjectMessages.DemoteProjectTab(id, name, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Demote tab")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["name"] = "The tab name.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid tab name.",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapGet("projects/check-name", async (string name, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, string? organizationId = null)
            => (await mediator.InvokeAsync<Result>(new ProjectMessages.CheckProjectName(name, organizationId, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status204NoContent)
        .WithSummary("Check for unique name")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["name"] = "The project name to check.",
            },
            ResponseDescriptions = new() {
                ["201"] = "The project name is available.",
                ["204"] = "The project name is not available.",
            }
        });

        group.MapGet("organizations/{organizationId:objectid}/projects/check-name", async (string organizationId, string name, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new ProjectMessages.CheckProjectName(name, organizationId, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status204NoContent)
        .WithSummary("Check for unique name")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["name"] = "The project name to check.",
                ["organizationId"] = "If set the check name will be scoped to a specific organization.",
            },
            ResponseDescriptions = new() {
                ["201"] = "The project name is available.",
                ["204"] = "The project name is not available.",
            }
        });

        group.MapPost("projects/{id:objectid}/data", async (string id, string key, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, [FromBody] ValueFromBody<string> value)
            => (await mediator.InvokeAsync<Result>(new ProjectMessages.SetProjectData(id, key, value, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<ValueFromBody<string>>("application/json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Add custom data")
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "Any string value.",
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["key"] = "The key name of the data object.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid key or value.",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapDelete("projects/{id:objectid}/data", async (string id, string key, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new ProjectMessages.DeleteProjectData(id, key, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Remove custom data")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["key"] = "The key name of the data object.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid key or value.",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapPost("projects/{id:objectid}/slack", async (string id, string code, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<object>>(new ProjectMessages.AddProjectSlack(id, code, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status304NotModified)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapDelete("projects/{id:objectid}/slack", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new ProjectMessages.RemoveProjectSlack(id, httpContext))).ToHttpResult(resultMapper))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<HttpIResult> UpdateProjectAsync(
        string id,
        HttpContext httpContext,
        IMediator mediator,
        IMediatorResultMapper<HttpIResult> resultMapper,
        IOptions<HttpJsonOptions> jsonOptions,
        [FromBody] JsonElement body)
    {
        var patchDocument = JsonPatchValidation.FromJsonBody<UpdateProject>(body, jsonOptions.Value.SerializerOptions);
        if (patchDocument is null)
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["patch"] = ["Invalid patch document."]
            });
        }

        return (await mediator.InvokeAsync<Result<ViewProject>>(new ProjectMessages.UpdateProjectMessage(id, patchDocument, httpContext))).ToHttpResult(resultMapper);
    }

}
