using System;

namespace Exceptionless.Core.Models.Billing {
    public class BillingPlanStats {
        public int SmallTotal { get; set; }
        public int SmallYearlyTotal { get; set; }
        public int MediumTotal { get; set; }
        public int MediumYearlyTotal { get; set; }
        public int LargeTotal { get; set; }
        public int LargeYearlyTotal { get; set; }
        public decimal MonthlyTotal { get; set; }
        public decimal YearlyTotal { get; set; }
        public int MonthlyTotalAccounts { get; set; }
        public int YearlyTotalAccounts { get; set; }
        public int FreeAccounts { get; set; }
        public int PaidAccounts { get; set; }
        public int FreeloaderAccounts { get; set; }
        public int SuspendedAccounts { get; set; }
    }
}