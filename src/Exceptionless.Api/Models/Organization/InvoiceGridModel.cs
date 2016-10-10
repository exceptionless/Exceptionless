using System;

namespace Exceptionless.Api.Models {
    public class InvoiceGridModel {
        public string Id { get; set; }
        public DateTime Date { get; set; }
        public bool Paid { get; set; }
    }
}