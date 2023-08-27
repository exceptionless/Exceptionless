namespace Exceptionless.Core.Messaging.Models;

public record PlanChanged
{
    public required string OrganizationId { get; set; }
}
