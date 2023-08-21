namespace Exceptionless.Core.Messaging.Models;

public record PlanOverage
{
    public required string OrganizationId { get; set; }
    public bool IsHourly { get; set; }
}
