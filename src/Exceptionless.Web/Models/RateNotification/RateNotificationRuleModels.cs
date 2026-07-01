using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Attributes;
using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public record NewRateNotificationRule
{
    [Required]
    [MaxLength(100)]
    public string Name { get; init; } = null!;

    public RateNotificationSignal Signal { get; init; }

    public RateNotificationSubject Subject { get; init; }

    [ObjectId]
    public string? StackId { get; init; }

    [Range(1, int.MaxValue)]
    public int Threshold { get; init; } = 10;

    public TimeSpan Window { get; init; } = TimeSpan.FromHours(1);

    public TimeSpan Cooldown { get; init; } = TimeSpan.FromHours(1);

    public bool IsEnabled { get; init; } = true;
}

public record UpdateRateNotificationRule
{
    [MaxLength(100)]
    public string? Name { get; init; }

    public RateNotificationSignal? Signal { get; init; }

    public RateNotificationSubject? Subject { get; init; }

    [ObjectId]
    public string? StackId { get; init; }

    [Range(1, int.MaxValue)]
    public int? Threshold { get; init; }

    public TimeSpan? Window { get; init; }

    public TimeSpan? Cooldown { get; init; }

    public bool? IsEnabled { get; init; }
}

public record ViewRateNotificationRule
{
    public string Id { get; init; } = null!;
    public string OrganizationId { get; init; } = null!;
    public string ProjectId { get; init; } = null!;
    public string UserId { get; init; } = null!;
    public int Version { get; init; }
    public string Name { get; init; } = null!;
    public bool IsEnabled { get; init; }
    public RateNotificationSignal Signal { get; init; }
    public RateNotificationSubject Subject { get; init; }
    public string? StackId { get; init; }
    public int Threshold { get; init; }
    public TimeSpan Window { get; init; }
    public TimeSpan Cooldown { get; init; }
    public DateTime? SnoozedUntilUtc { get; init; }
    public bool IsSnoozed { get; init; }
    public DateTime? LastFiredUtc { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
}

public record SnoozeRateNotificationRuleRequest
{
    /// <summary>Snooze duration in seconds. Mutually exclusive with UntilUtc.</summary>
    [Range(1, int.MaxValue)]
    public int? DurationSeconds { get; init; }

    /// <summary>Snooze until this UTC timestamp. Mutually exclusive with DurationSeconds.</summary>
    public DateTime? UntilUtc { get; init; }
}
