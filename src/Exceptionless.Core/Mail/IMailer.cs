using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Mail {
    public interface IMailer {
        Task SendEventNoticeAsync(User user, PersistentEvent ev, Project project, bool isNew, bool isRegression, int totalOccurrences);
        Task SendOrganizationAddedAsync(User sender, Organization organization, User user);
        Task SendOrganizationInviteAsync(User sender, Organization organization, Invite invite);
        Task SendOrganizationNoticeAsync(User user, Organization organization, bool isOverMonthlyLimit, bool isOverHourlyLimit);
        Task SendOrganizationPaymentFailedAsync(User owner, Organization organization);
        Task SendProjectDailySummaryAsync(User user, Project project, DateTime startDate, bool hasSubmittedEvents, long total, double uniqueTotal, double newTotal, bool isFreePlan);
        Task SendUserEmailVerifyAsync(User user);
        Task SendUserPasswordResetAsync(User user);
    }
}