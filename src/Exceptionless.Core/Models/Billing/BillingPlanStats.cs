namespace Exceptionless.Core.Models.Billing;

public record BillingPlanStats
{
    public int SmallTotal { get; init; }
    public int SmallYearlyTotal { get; init; }
    public int MediumTotal { get; init; }
    public int MediumYearlyTotal { get; init; }
    public int LargeTotal { get; init; }
    public int LargeYearlyTotal { get; init; }
    public decimal MonthlyTotal { get; init; }
    public decimal YearlyTotal { get; init; }
    public int MonthlyTotalAccounts { get; init; }
    public int YearlyTotalAccounts { get; init; }
    public int FreeAccounts { get; init; }
    public int PaidAccounts { get; init; }
    public int FreeloaderAccounts { get; init; }
    public int SuspendedAccounts { get; init; }
}
