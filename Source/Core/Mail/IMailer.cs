using System;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Mail {
    public interface IMailer {
        void SendPasswordReset(User user);
        void SendVerifyEmail(User user);
        void SendInvite(User sender, Organization organization, Invite invite);
        void SendPaymentFailed(User owner, Organization organization);
        void SendAddedToOrganization(User sender, Organization organization, User user);
        void SendNotice(string emailAddress, EventNotification model);
        void SendDailySummary(string emailAddress, DailySummaryModel notification);
    }
}