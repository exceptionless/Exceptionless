using Exceptionless.Web.Models;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace Exceptionless.Web.Api.Messages;

public record GetCurrentUser;
public record GetUserById(string Id);
public record GetUsersByOrganization(string OrganizationId, int Page, int Limit);
public record UpdateUserMessage(string Id, JsonPatchDocument<UpdateUser> PatchDocument);
public record SetUserAvatar(string Id, string FileName);
public record DeleteUserAvatar(string Id);
public record DeleteCurrentUser;
public record DeleteUsers(string[] Ids);
public record UpdateEmailAddress(string Id, string Email);
public record VerifyEmailAddress(string Token);
public record ResendVerificationEmail(string Id);
public record UnverifyEmailAddresses;
public record AddAdminRole(string Id);
public record RemoveAdminRole(string Id);
