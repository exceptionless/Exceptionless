using System;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Mail.Models {
    public class OrganizationNotificationModel : MailModelBase {
        public Organization Organization { get; set; }
        public bool IsOverHourlyLimit { get; set; }
        public bool IsOverMonthlyLimit { get; set; }
    }
}