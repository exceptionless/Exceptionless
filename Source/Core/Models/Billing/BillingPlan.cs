#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.Core.Models.Billing {
    public class BillingPlan {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int MaxProjects { get; set; }
        public int MaxUsers { get; set; }
        public int RetentionDays { get; set; }
        public int MaxErrorsPerDay { get; set; }
        public bool HasPremiumFeatures { get; set; }
        public bool IsHidden { get; set; }
    }
}