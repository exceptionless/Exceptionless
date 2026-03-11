namespace Exceptionless.Core.Models.WorkItems;

public record UpdateProjectNotificationSettingsWorkItem
{
    public string? OrganizationId { get; init; }
}
