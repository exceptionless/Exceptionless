using Foundatio.Queues;

namespace Exceptionless.Core.Models.WorkItems;

public record OrganizationNotificationWorkItem : IHaveUniqueIdentifier
{
    public const string HourlyNotificationType = "hourly";
    public const string MonthlyNotificationType = "monthly";

    public required string OrganizationId { get; init; }
    public required bool IsOverHourlyLimit { get; init; }
    public required bool IsOverMonthlyLimit { get; init; }

    public string UniqueIdentifier => GetNotificationKey(OrganizationId, IsOverMonthlyLimit);

    public static string GetNotificationKey(string organizationId, bool isOverMonthlyLimit)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        return $"Organization:{organizationId}:notification:{(isOverMonthlyLimit ? MonthlyNotificationType : HourlyNotificationType)}";
    }
}
