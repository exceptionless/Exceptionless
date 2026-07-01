using Exceptionless.Web.Models;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace Exceptionless.Web.Api.Messages;

public record GetTokensByOrganization(string OrganizationId, int Page, int Limit);
public record GetTokensByProject(string ProjectId, int Page, int Limit);
public record GetDefaultToken(string ProjectId);
public record GetTokenById(string Id);
public record CreateToken(NewToken Token);
public record CreateTokenByProject(string ProjectId, NewToken? Token);
public record CreateTokenByOrganization(string OrganizationId, NewToken? Token);
public record UpdateTokenMessage(string Id, JsonPatchDocument<UpdateToken> PatchDocument);
public record DeleteTokens(string[] Ids);
