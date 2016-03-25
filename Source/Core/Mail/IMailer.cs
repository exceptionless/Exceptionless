using System;
using System.Threading.Tasks;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Mail {
    public interface IMailer {
        Task SendPasswordResetAsync(User user);
        Task SendVerifyEmailAsync(User user);
        Task SendInviteAsync(User sender, Organization organization, Invite invite);
        Task SendPaymentFailedAsync(User owner, Organization organization);
        Task SendAddedToOrganizationAsync(User sender, Organization organization, User user);
        Task SendEventNoticeAsync(string emailAddress, EventNotification model);
        Task SendOrganizationNoticeAsync(string emailAddress, OrganizationNotificationModel organizationNotificationModel);
        Task SendDailySummaryAsync(string emailAddress, DailySummaryModel notification);
    }
}