using Exceptionless.Web.Models;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace Exceptionless.Web.Api.Messages;

public record GetSavedViewsByOrganization(string OrganizationId, int Page, int Limit);
public record GetSavedViewsByView(string OrganizationId, string ViewType, int Page, int Limit);
public record GetSavedViewById(string Id);
public record CreateSavedView(string OrganizationId, NewSavedView SavedView);
public record CreatePredefinedSavedViews(string OrganizationId);
public record GetPredefinedSavedViews;
public record PromoteToPredefinedSavedView(string Id);
public record DeletePredefinedSavedView(string Id);
public record UpdateSavedViewMessage(string Id, JsonPatchDocument<UpdateSavedView> PatchDocument);
public record DeleteSavedViews(string[] Ids);
