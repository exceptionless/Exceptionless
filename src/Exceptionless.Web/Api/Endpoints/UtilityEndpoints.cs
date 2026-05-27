using Exceptionless.Core.Authorization;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Messages;
using Foundatio.Mediator;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Exceptionless.Web.Api.Endpoints;

public static class UtilityEndpoints
{
    public static IEndpointRouteBuilder MapUtilityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .ExcludeFromDescription();

        group.MapGet("search/validate", async (IMediator mediator, string query) =>
        {
            if (String.IsNullOrEmpty(query))
                return HttpResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["query"] = ["The query field is required."]
                });

            var result = await mediator.InvokeAsync<AppQueryValidator.QueryProcessResult>(new ValidateSearchQuery(query));
            return HttpResults.Ok(result);
        });

        return endpoints;
    }
}
