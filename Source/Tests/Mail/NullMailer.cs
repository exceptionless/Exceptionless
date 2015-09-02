using System;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Models;

namespace Exceptionless.Api.Tests.Mail {
    public class NullMailer : IMailer {
        public void SendPasswordReset(User user) {}
        public void SendVerifyEmail(User user) {}
        public void SendInvite(User sender, Organization organization, Invite invite) {}
        public void SendPaymentFailed(User owner, Organization organization) {}
        public void SendAddedToOrganization(User sender, Organization organization, User user) {}
        public void SendNotice(string emailAddress, EventNotification model) {}
        public void SendDailySummary(string emailAddress, DailySummaryModel notification) {}
    }
}