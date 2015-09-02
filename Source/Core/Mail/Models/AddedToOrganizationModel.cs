using System;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Mail.Models {
    public class AddedToOrganizationModel : MailModelBase {
        public User Sender { get; set; }
        public Organization Organization { get; set; }
        public User User { get; set; }
    }
}