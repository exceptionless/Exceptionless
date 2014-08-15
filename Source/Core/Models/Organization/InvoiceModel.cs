#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Stripe;

namespace Exceptionless.App.Models.Organization {
    public class InvoiceModel {
        public Exceptionless.Models.Organization Organization { get; set; }
        public StripeInvoice Invoice { get; set; }
        public string Id { get { return Invoice.Id.Substring(3); } }
    }
}