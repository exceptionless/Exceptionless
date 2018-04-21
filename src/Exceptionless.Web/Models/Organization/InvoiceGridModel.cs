using System;

namespace Exceptionless.Web.Models {
    public class InvoiceGridModel {
        public string Id { get; set; }
        public DateTime Date { get; set; }
        public bool Paid { get; set; }
    }
}