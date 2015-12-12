using System;
using System.Threading.Tasks;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Models;

namespace Exceptionless.Api.Tests.Mail {
    public class NullMailer : IMailer {
        public Task SendPasswordResetAsync(User user) {
            return Task.CompletedTask;
        }

        public Task SendVerifyEmailAsync(User user) {
            return Task.CompletedTask;
        }

        public Task SendInviteAsync(User sender, Organization organization, Invite invite) {
            return Task.CompletedTask;
        }

        public Task SendPaymentFailedAsync(User owner, Organization organization) {
            return Task.CompletedTask;
        }

        public Task SendAddedToOrganizationAsync(User sender, Organization organization, User user) {
            return Task.CompletedTask;
        }

        public Task SendNoticeAsync(string emailAddress, EventNotification model) {
            return Task.CompletedTask;
        }

        public Task SendDailySummaryAsync(string emailAddress, DailySummaryModel notification) {
            return Task.CompletedTask;
        }
    }
}