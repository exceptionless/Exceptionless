using Exceptionless.Web.Models;

namespace Exceptionless.Web.Api.Messages;

public record CreateBulkOperation(NewBulkOperation Operation);
public record GetBackgroundTaskById(string Id);
public record GetBackgroundTasksByOrganization(string OrganizationId);
public record CancelBackgroundTask(string Id);
