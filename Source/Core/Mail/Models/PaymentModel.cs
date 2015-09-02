using System;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Mail.Models {
    public class PaymentModel : MailModelBase {
        public User Owner { get; set; }
        public Organization Organization { get; set; }
    }
}