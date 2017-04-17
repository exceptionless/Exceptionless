using System;
using System.Threading.Tasks;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Models;

namespace Exceptionless.Api.Tests.Mail {
    public class NullMailer : IMailer {
        public Task SendEventNoticeAsync(User user, EventNotification model) {
            return Task.CompletedTask;
        }

        public Task SendOrganizationAddedAsync(User sender, Organization organization, User user) {
            return Task.CompletedTask;
        }

        public Task SendOrganizationInviteAsync(User sender, Organization organization, Invite invite) {
            return Task.CompletedTask;
        }

        public Task SendOrganizationNoticeAsync(User user, Organization organization, bool isOverMonthlyLimit, bool isOverHourlyLimit) {
            return Task.CompletedTask;
        }

        public Task SendOrganizationPaymentFailedAsync(User owner, Organization organization) {
            return Task.CompletedTask;
        }

        public Task SendProjectDailySummaryAsync(User user, Project project, DateTime startDate, bool hasSubmittedEvents, long total, double uniqueTotal, double newTotal, bool isFreePlan) {
            return Task.CompletedTask;
        }

        public Task SendUserEmailVerifyAsync(User user) {
            return Task.CompletedTask;
        }

        public Task SendUserPasswordResetAsync(User user) {
            return Task.CompletedTask;
        }
    }
}