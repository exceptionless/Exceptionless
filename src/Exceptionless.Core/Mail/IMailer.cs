using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Mail {
    public interface IMailer {
        Task<bool> SendEventNoticeAsync(User user, PersistentEvent ev, Project project, bool isNew, bool isRegression, int totalOccurrences);
        Task SendOrganizationAddedAsync(User sender, Organization organization, User user);
        Task SendOrganizationInviteAsync(User sender, Organization organization, Invite invite);
        Task SendOrganizationNoticeAsync(User user, Organization organization, bool isOverMonthlyLimit, bool isOverHourlyLimit);
        Task SendOrganizationPaymentFailedAsync(User owner, Organization organization);
        Task SendProjectDailySummaryAsync(User user, Project project, IEnumerable<Stack> mostFrequent, IEnumerable<Stack> newest, DateTime startDate, bool hasSubmittedEvents, double count, double uniqueCount, double newCount, double fixedCount, int blockedCount, int tooBigCount, bool isFreePlan);
        Task SendUserEmailVerifyAsync(User user);
        Task SendUserPasswordResetAsync(User user);
    }
}