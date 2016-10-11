using System;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Mail.Models {
    public class UserModel : MailModelBase {
        public User User { get; set; }
    }
}