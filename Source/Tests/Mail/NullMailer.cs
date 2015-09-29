using System;
using System.Threading.Tasks;
using Exceptionless.Core.Component;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Models;

namespace Exceptionless.Api.Tests.Mail {
    public class NullMailer : IMailer {
        public Task SendPasswordResetAsync(User user) {
            return TaskHelper.Completed();
        }

        public Task SendVerifyEmailAsync(User user) {
            return TaskHelper.Completed();
        }

        public Task SendInviteAsync(User sender, Organization organization, Invite invite) {
            return TaskHelper.Completed();
        }

        public Task SendPaymentFailedAsync(User owner, Organization organization) {
            return TaskHelper.Completed();
        }

        public Task SendAddedToOrganizationAsync(User sender, Organization organization, User user) {
            return TaskHelper.Completed();
        }

        public Task SendNoticeAsync(string emailAddress, EventNotification model) {
            return TaskHelper.Completed();
        }

        public Task SendDailySummaryAsync(string emailAddress, DailySummaryModel notification) {
            return TaskHelper.Completed();
        }
    }
}