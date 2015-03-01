#region Copyright 2015 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

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