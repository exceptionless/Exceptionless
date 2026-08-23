using Exceptionless.Core.Seed;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;

namespace Exceptionless.Web.Api.Messages;

public record GetSavedViewsByOrganization(string OrganizationId, int Page, int Limit);
public record GetSavedViewsByView(string OrganizationId, string ViewType, int Page, int Limit);
public record GetSavedViewById(string Id);
public record GetSavedViewDefaults(string OrganizationId);
public record UpdateUserSavedViewDefault(string OrganizationId, UpdateSavedViewDefault Default);
public record UpdateOrganizationSavedViewDefault(string OrganizationId, UpdateSavedViewDefault Default);
public record CreateSavedView(string OrganizationId, NewSavedView SavedView);
public record CreatePredefinedSavedViews(string OrganizationId);
public record GetPredefinedSavedViews;
public record ExportOrganizationSavedViews(string OrganizationId);
public record ReplacePredefinedSavedViews(IReadOnlyCollection<PredefinedSavedViewDefinition> Definitions);
public record ForceUpdatePredefinedSavedViews;
public record PromoteToPredefinedSavedView(string Id);
public record DeletePredefinedSavedView(string Id);
public record UpdateSavedViewMessage(string Id, Delta<UpdateSavedView> Changes);
public record DeleteSavedViews(string[] Ids);
