using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class BillingOptions
{
    /// <summary>
    /// Length of the no-card trial a user's first organization receives. Zero disables trials.
    /// </summary>
    public int TrialDays { get; internal set; } = 14;

    /// <summary>
    /// Days before a trial ends to send the trial ending reminder email.
    /// </summary>
    public int TrialReminderDays { get; internal set; } = 3;

    /// <summary>
    /// Combined daily Exie cost limit across all trial organizations. Zero or less disables the limit.
    /// </summary>
    public decimal TrialAssistantDailyCostLimitUsd { get; internal set; } = 25m;

    /// <summary>
    /// Maximum organization invites a user can send per UTC day from free or trial organizations.
    /// </summary>
    public int FreeInviteDailyLimit { get; internal set; } = 10;

    /// <summary>
    /// Maximum organization invites a user can send per UTC day from paid organizations.
    /// </summary>
    public int PaidInviteDailyLimit { get; internal set; } = 50;

    public static BillingOptions ReadFromConfiguration(IConfiguration config)
    {
        var options = new BillingOptions();
        options.TrialDays = Math.Max(0, config.GetValue("Billing:TrialDays", 14));
        options.TrialReminderDays = Math.Max(0, config.GetValue("Billing:TrialReminderDays", 3));
        options.TrialAssistantDailyCostLimitUsd = config.GetValue("Billing:TrialAssistantDailyCostLimitUsd", 25m);
        options.FreeInviteDailyLimit = Math.Max(0, config.GetValue("Billing:FreeInviteDailyLimit", 10));
        options.PaidInviteDailyLimit = Math.Max(0, config.GetValue("Billing:PaidInviteDailyLimit", 50));
        return options;
    }
}
