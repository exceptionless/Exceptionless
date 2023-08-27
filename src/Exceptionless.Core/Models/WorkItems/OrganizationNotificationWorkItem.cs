namespace Exceptionless.Core.Models.WorkItems;

public record OrganizationNotificationWorkItem
{
    public required string OrganizationId { get; init; }
    public required bool IsOverHourlyLimit { get; init; }
    public required bool IsOverMonthlyLimit { get; init; }
}
