using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;

namespace Exceptionless.Tests.Mail;

public class CountingMailer : IMailer
{
    private int _organizationNoticeCount;

    public int OrganizationNoticeCount => _organizationNoticeCount;

    public List<OrganizationNoticeCall> OrganizationNoticeCalls { get; } = [];

    /// <summary>
    /// When true, <see cref="SendOrganizationNoticeAsync"/> throws instead of recording a call.
    /// Reset by <see cref="Reset"/>.
    /// </summary>
    public bool ShouldThrow { get; set; }

    public Task<bool> SendEventNoticeAsync(User user, PersistentEvent ev, Project project, bool isNew, bool isRegression, int totalOccurrences)
    {
        return Task.FromResult(true);
    }

    public Task SendOrganizationAddedAsync(User sender, Organization organization, User user)
    {
        return Task.CompletedTask;
    }

    public Task SendOrganizationInviteAsync(User sender, Organization organization, Invite invite)
    {
        return Task.CompletedTask;
    }

    public Task SendOrganizationNoticeAsync(User user, Organization organization, bool isOverMonthlyLimit, bool isOverHourlyLimit)
    {
        if (ShouldThrow)
            throw new InvalidOperationException("Simulated mailer failure.");

        Interlocked.Increment(ref _organizationNoticeCount);
        lock (OrganizationNoticeCalls)
        {
            OrganizationNoticeCalls.Add(new OrganizationNoticeCall(user.Id, organization.Id, isOverMonthlyLimit, isOverHourlyLimit));
        }
        return Task.CompletedTask;
    }

    public Task SendOrganizationPaymentFailedAsync(User owner, Organization organization)
    {
        return Task.CompletedTask;
    }

    public Task SendProjectDailySummaryAsync(User user, Project project, IEnumerable<Stack>? mostFrequent, IEnumerable<Stack>? newest, DateTime startDate, bool hasSubmittedEvents, double count, double uniqueCount, double newCount, double fixedCount, int blockedCount, int tooBigCount, bool isFreePlan)
    {
        return Task.CompletedTask;
    }

    public Task SendUserEmailVerifyAsync(User user)
    {
        return Task.CompletedTask;
    }

    public Task SendUserPasswordResetAsync(User user)
    {
        return Task.CompletedTask;
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _organizationNoticeCount, 0);
        lock (OrganizationNoticeCalls)
        {
            OrganizationNoticeCalls.Clear();
        }
        ShouldThrow = false;
    }
}

public record OrganizationNoticeCall(string UserId, string OrganizationId, bool IsOverMonthlyLimit, bool IsOverHourlyLimit);
