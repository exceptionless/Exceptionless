using System;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Mail.Models {
    public class InviteModel : MailModelBase {
        public User Sender { get; set; }
        public Organization Organization { get; set; }
        public Invite Invite { get; set; }
    }
}