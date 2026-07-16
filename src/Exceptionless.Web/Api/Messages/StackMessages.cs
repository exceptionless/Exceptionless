using System.Text.Json;
using Exceptionless.Core.Models;
using Exceptionless.Web.Models;

namespace Exceptionless.Web.Api.Messages;

public record GetStackById(string Id, string? Offset, HttpContext Context);
public record MarkStacksFixed(string Ids, string? Version, HttpContext Context);
public record MarkStacksFixedByZapier(JsonDocument Data, HttpContext Context);
public record SnoozeStacks(string Ids, DateTime SnoozeUntilUtc, HttpContext Context);
public record AddStackLink(string Id, ValueFromBody<string?> Url, HttpContext Context);
public record AddStackLinkByZapier(JsonDocument Data, HttpContext Context);
public record RemoveStackLink(string Id, ValueFromBody<string> Url, HttpContext Context);
public record MarkStacksCritical(string Ids, HttpContext Context);
public record MarkStacksNotCritical(string Ids, HttpContext Context);
public record ChangeStacksStatus(string Ids, StackStatus Status, HttpContext Context);
public record PromoteStack(string Id, HttpContext Context);
public record DeleteStacks(string Ids, HttpContext Context);
public record GetAllStacks(string? Filter, string? Sort, string? Time, string? Offset, string? Mode, int Page, int Limit, HttpContext Context);
public record GetStacksByOrganization(string OrganizationId, string? Filter, string? Sort, string? Time, string? Offset, string? Mode, int Page, int Limit, HttpContext Context);
public record GetStacksByProject(string ProjectId, string? Filter, string? Sort, string? Time, string? Offset, string? Mode, int Page, int Limit, HttpContext Context);
