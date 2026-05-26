using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using IMediator = Foundatio.Mediator.IMediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TokenMessages = Exceptionless.Web.Api.Messages;

namespace Exceptionless.Web.Api.Endpoints;

public static class TokenEndpoints
{
    public static IEndpointRouteBuilder MapTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy);

        group.MapGet("organizations/{organizationId:objectid}/tokens", async (string organizationId, IMediator mediator, int page = 1, int limit = 10)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new TokenMessages.GetTokensByOrganization(organizationId, page, limit)));

        group.MapGet("projects/{projectId:objectid}/tokens", async (string projectId, IMediator mediator, int page = 1, int limit = 10)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new TokenMessages.GetTokensByProject(projectId, page, limit)));

        group.MapGet("projects/{projectId:objectid}/tokens/default", async (string projectId, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new TokenMessages.GetDefaultToken(projectId)));

        group.MapGet("tokens/{id}", async (string id, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new TokenMessages.GetTokenById(id)))
        .WithName("GetTokenById");

        group.MapPost("tokens", async (IMediator mediator, IServiceProvider serviceProvider, [FromBody] NewToken token) =>
        {
            var validation = await ApiValidation.ValidateAsync(token, serviceProvider);
            if (validation is not null)
                return validation;

            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new TokenMessages.CreateToken(token));
        });

        group.MapPost("projects/{projectId:objectid}/tokens", async (string projectId, IMediator mediator, IServiceProvider serviceProvider,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NewToken? token = null) =>
        {
            if (token is not null)
            {
                var validation = await ApiValidation.ValidateAsync(token, serviceProvider);
                if (validation is not null)
                    return validation;
            }

            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new TokenMessages.CreateTokenByProject(projectId, token));
        });

        group.MapPost("organizations/{organizationId:objectid}/tokens", async (string organizationId, IMediator mediator, IServiceProvider serviceProvider,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NewToken? token = null) =>
        {
            if (token is not null)
            {
                var validation = await ApiValidation.ValidateAsync(token, serviceProvider);
                if (validation is not null)
                    return validation;
            }

            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new TokenMessages.CreateTokenByOrganization(organizationId, token));
        });

        group.MapPatch("tokens/{id}", async (string id, IMediator mediator, [FromBody] Delta<UpdateToken> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new TokenMessages.UpdateTokenMessage(id, changes)));

        group.MapPut("tokens/{id}", async (string id, IMediator mediator, [FromBody] Delta<UpdateToken> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new TokenMessages.UpdateTokenMessage(id, changes)));

        group.MapDelete("tokens/{ids}", async (string ids, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new TokenMessages.DeleteTokens(ids.FromDelimitedString())));

        return endpoints;
    }
}
