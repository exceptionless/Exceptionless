namespace Exceptionless.Web.Api.Messages;

public record GetAdminSettings;
public record GetAdminStats;
public record GetAdminMigrations;
public record GetAdminEcho(HttpContext Context);
public record GetAdminAssemblies;
public record AdminChangePlan(string OrganizationId, string PlanId, HttpContext Context);
public record AdminSetBonus(string OrganizationId, int BonusEvents, DateTime? Expires, HttpContext Context);
public record AdminRequeue(string? Path = null, bool Archive = false);
public record AdminRunMaintenance(string Name, DateTime? UtcStart, DateTime? UtcEnd, string? OrganizationId);
public record GetAdminElasticsearch;
public record GetAdminElasticsearchSnapshots;
public record AdminGenerateSampleEvents(int EventCount, int DaysBack);
