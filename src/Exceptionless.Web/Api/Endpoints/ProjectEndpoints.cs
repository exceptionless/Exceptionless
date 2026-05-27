using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Extensions;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using IMediator = Foundatio.Mediator.IMediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ProjectMessages = Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Utility.OpenApi;

namespace Exceptionless.Web.Api.Endpoints;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.ClientPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("Projects");

        group.MapGet("projects", async (HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, int page = 1, int limit = 10, string? mode = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.GetProjects(filter, sort, page, limit, mode, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
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

        group.MapGet("organizations/{organizationId:objectid}/projects", async (string organizationId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, int page = 1, int limit = 10, string? mode = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.GetProjectsByOrganization(organizationId, filter, sort, page, limit, mode, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
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

        group.MapGet("projects/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, string? mode = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.GetProjectById(id, mode, httpContext)))
        .WithName("GetProjectById")
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
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

        group.MapPost("projects", async (HttpContext httpContext, IMediator mediator, IServiceProvider serviceProvider, [FromBody] NewProject project) =>
        {
            var validation = await ApiValidation.ValidateAsync(project, serviceProvider);
            if (validation is not null)
                return validation;

            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.CreateProject(project, httpContext));
        })
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<NewProject>("application/json")
        .Produces<ViewProject>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Create")
        .WithMetadata(new EndpointDocumentation {
            ResponseDescriptions = new() {
                ["201"] = "Created",
                ["400"] = "An error occurred while creating the project.",
                ["409"] = "The project already exists.",
            }
        });

        group.MapPatch("projects/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, [FromBody] Delta<UpdateProject> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.UpdateProjectMessage(id, changes, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<Delta<UpdateProject>>("application/json")
        .Produces<ViewProject>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Update")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the project.",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapPut("projects/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, [FromBody] Delta<UpdateProject> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.UpdateProjectMessage(id, changes, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<Delta<UpdateProject>>("application/json")
        .Produces<ViewProject>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Update")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
            },
            ResponseDescriptions = new() {
                ["400"] = "An error occurred while updating the project.",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapDelete("projects/{ids:objectids}", async (string ids, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.DeleteProjects(ids.FromDelimitedString(), httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
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

        endpoints.MapGet("api/v1/project/config", async (HttpContext httpContext, IMediator mediator, int? v = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.GetLegacyProjectConfig(v, httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .WithTags("Projects")
        .Produces<ClientConfiguration>()
        .Produces(StatusCodes.Status304NotModified)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("projects/config", async (HttpContext httpContext, IMediator mediator, int? v = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.GetProjectConfig(null, v, httpContext)))
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

        group.MapGet("projects/{id:objectid}/config", async (string id, HttpContext httpContext, IMediator mediator, int? v = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.GetProjectConfig(id, v, httpContext)))
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

        group.MapPost("projects/{id:objectid}/config", async (string id, string key, HttpContext httpContext, IMediator mediator, [FromBody] ValueFromBody<string> value)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.SetProjectConfig(id, key, value, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<ValueFromBody<string>>("application/json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Add configuration value")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["key"] = "The key name of the configuration object.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid configuration value.",
                ["404"] = "The project could not be found.",
            }
        });

        group.MapDelete("projects/{id:objectid}/config", async (string id, string key, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.DeleteProjectConfig(id, key, httpContext)))
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

        group.MapPost("projects/{id:objectid}/sample-data", async (string id, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.GenerateProjectSampleData(id, httpContext)))
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

        group.MapGet("projects/{id:objectid}/reset-data", async (string id, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.ResetProjectData(id, httpContext)))
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

        group.MapPost("projects/{id:objectid}/reset-data", async (string id, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.ResetProjectData(id, httpContext)))
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

        group.MapGet("projects/{id:objectid}/notifications", async (string id, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.GetProjectNotificationSettings(id, httpContext)))
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
        .Produces<IDictionary<string, NotificationSettings>>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapGet("users/{userId:objectid}/projects/{id:objectid}/notifications", async (string id, string userId, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.GetProjectUserNotificationSettings(id, userId, httpContext)))
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

        group.MapGet("projects/{id:objectid}/{integration:minlength(1)}/notifications", async (string id, string integration, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.GetProjectIntegrationNotificationSettings(id, integration, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<NotificationSettings>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapPut("users/{userId:objectid}/projects/{id:objectid}/notifications", async (string id, string userId, HttpContext httpContext, IMediator mediator,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NotificationSettings? settings = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.SetProjectUserNotificationSettings(id, userId, settings, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<NotificationSettings>("application/json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Set user notification settings")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["userId"] = "The identifier of the user.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The project could not be found.",
            }
        });

        group.MapPost("users/{userId:objectid}/projects/{id:objectid}/notifications", async (string id, string userId, HttpContext httpContext, IMediator mediator,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NotificationSettings? settings = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.SetProjectUserNotificationSettings(id, userId, settings, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<NotificationSettings>("application/json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Set user notification settings")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["userId"] = "The identifier of the user.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The project could not be found.",
            }
        });

        group.MapPut("projects/{id:objectid}/{integration:minlength(1)}/notifications", async (string id, string integration, HttpContext httpContext, IMediator mediator,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NotificationSettings? settings = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.SetProjectIntegrationNotificationSettings(id, integration, settings, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<NotificationSettings>("application/json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Set an integrations notification settings")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["integration"] = "The identifier of the integration.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The project or integration could not be found.",
                ["426"] = "Please upgrade your plan to enable integrations.",
            }
        });

        group.MapPost("projects/{id:objectid}/{integration:minlength(1)}/notifications", async (string id, string integration, HttpContext httpContext, IMediator mediator,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NotificationSettings? settings = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.SetProjectIntegrationNotificationSettings(id, integration, settings, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<NotificationSettings>("application/json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Set an integrations notification settings")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the project.",
                ["integration"] = "The identifier of the integration.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The project or integration could not be found.",
                ["426"] = "Please upgrade your plan to enable integrations.",
            }
        });

        group.MapDelete("users/{userId:objectid}/projects/{id:objectid}/notifications", async (string id, string userId, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.DeleteProjectNotificationSettings(id, userId, httpContext)))
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

        group.MapPut("projects/{id:objectid}/promotedtabs", async (string id, string name, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.PromoteProjectTab(id, name, httpContext)))
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

        group.MapPost("projects/{id:objectid}/promotedtabs", async (string id, string name, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.PromoteProjectTab(id, name, httpContext)))
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

        group.MapDelete("projects/{id:objectid}/promotedtabs", async (string id, string name, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.DemoteProjectTab(id, name, httpContext)))
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

        group.MapGet("projects/check-name", async (string name, HttpContext httpContext, IMediator mediator, string? organizationId = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.CheckProjectName(name, organizationId, httpContext)))
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

        group.MapGet("organizations/{organizationId:objectid}/projects/check-name", async (string organizationId, string name, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.CheckProjectName(name, organizationId, httpContext)))
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

        group.MapPost("projects/{id:objectid}/data", async (string id, string key, HttpContext httpContext, IMediator mediator, [FromBody] ValueFromBody<string> value)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.SetProjectData(id, key, value, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<ValueFromBody<string>>("application/json")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Add custom data")
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

        group.MapDelete("projects/{id:objectid}/data", async (string id, string key, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.DeleteProjectData(id, key, httpContext)))
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

        group.MapPost("projects/{id:objectid}/slack", async (string id, string code, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.AddProjectSlack(id, code, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status304NotModified)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        group.MapDelete("projects/{id:objectid}/slack", async (string id, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ProjectMessages.RemoveProjectSlack(id, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ExcludeFromDescription();

        return endpoints;
    }
}
