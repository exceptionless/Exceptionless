namespace Exceptionless.Core.Models.WorkItems;

public record UserMaintenanceWorkItem
{
    public bool Normalize { get; init; }
    public bool ResetVerifyEmailAddressToken { get; init; }
}
