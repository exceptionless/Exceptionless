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
    /// Dedicated base64-encoded secret of at least 32 random bytes for mailbox trial claims. Keep stable across hosts and deployments.
    /// </summary>
    public string? TrialClaimSecret { get; internal set; }

    /// <summary>
    /// Days a mailbox's trial claim blocks another trial. After that the mailbox can trial again and the claim is removed.
    /// </summary>
    public int TrialClaimRetentionDays { get; internal set; } = 365;

    public static BillingOptions ReadFromConfiguration(IConfiguration config)
    {
        var options = new BillingOptions();
        options.TrialDays = Math.Max(0, config.GetValue("Billing:TrialDays", 14));
        options.TrialReminderDays = Math.Max(0, config.GetValue("Billing:TrialReminderDays", 3));
        options.TrialAssistantDailyCostLimitUsd = config.GetValue("Billing:TrialAssistantDailyCostLimitUsd", 25m);
        options.FreeInviteDailyLimit = Math.Max(0, config.GetValue("Billing:FreeInviteDailyLimit", 10));
        options.TrialClaimRetentionDays = Math.Max(1, config.GetValue("Billing:TrialClaimRetentionDays", 365));
        options.TrialClaimSecret = config.GetValue<string>("Billing:TrialClaimSecret");
        if (!String.IsNullOrEmpty(options.TrialClaimSecret))
        {
            byte[] key;
            try
            {
                key = Convert.FromBase64String(options.TrialClaimSecret);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Billing:TrialClaimSecret must be base64-encoded random bytes");
            }

            if (key.Length is < 32)
                throw new InvalidOperationException("Billing:TrialClaimSecret must contain at least 32 random bytes");
        }
        else
        {
            options.TrialClaimSecret = null;
        }

        return options;
    }
}
