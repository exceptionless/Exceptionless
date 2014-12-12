using System;

namespace Exceptionless.Api.Models {
    public class InvoiceLineItem {
        public string Description { get; set; }
        public string Date { get; set; }
        public double Amount { get; set; }
    }
}