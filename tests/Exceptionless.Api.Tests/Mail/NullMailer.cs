using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;

namespace Exceptionless.Api.Tests.Mail {
    public class NullMailer : IMailer {
        public Task<bool> SendEventNoticeAsync(User user, PersistentEvent ev, Project project, bool isNew, bool isRegression, int totalOccurrences) {
            return Task.FromResult(true);
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

        public Task SendProjectDailySummaryAsync(User user, Project project, IEnumerable<Stack> mostFrequent, IEnumerable<Stack> newest, DateTime startDate, bool hasSubmittedEvents, double count, double uniqueCount, double newCount, double fixedCount, int blockedCount, int tooBigCount, bool isFreePlan) {
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