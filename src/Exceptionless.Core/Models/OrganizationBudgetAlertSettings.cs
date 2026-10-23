namespace Exceptionless.Core.Models;

public class OrganizationBudgetAlertSettings
{
    public bool Enabled { get; set; }

    /// <summary>
    /// Percentage thresholds of the organization's effective monthly event allowance.
    /// Example: [50, 80, 90].
    /// </summary>
    public SortedSet<int> Thresholds { get; set; } = [];
}
