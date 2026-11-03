using Exceptionless.Web.Models;

namespace Exceptionless.Web.Api.Messages;

public record CreateBulkOperation(NewBulkOperation Operation);
public record GetBulkOperationById(string Id);
public record GetBulkOperationsByOrganization(string OrganizationId);
public record CancelBulkOperation(string Id);
