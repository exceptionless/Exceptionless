using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Attributes;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

public class RateNotificationRule : IOwnedByOrganizationAndProjectWithIdentity, IHaveDates
{
    public static TimeSpan MaximumWindow { get; } = TimeSpan.FromHours(1);
    public static TimeSpan MaximumCooldown { get; } = TimeSpan.FromDays(1);

    [ObjectId]
    public string Id { get; set; } = null!;

    [Required]
    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    [Required]
    [ObjectId]
    public string ProjectId { get; set; } = null!;

    [Required]
    [ObjectId]
    public string UserId { get; set; } = null!;

    public int Version { get; set; } = 1;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    public bool IsEnabled { get; set; } = true;

    [EnumDataType(typeof(RateNotificationSignal))]
    public RateNotificationSignal Signal { get; set; }

    [EnumDataType(typeof(RateNotificationSubject))]
    public RateNotificationSubject Subject { get; set; }

    [ObjectId]
    public string? StackId { get; set; }

    [Range(1, int.MaxValue)]
    public int Threshold { get; set; } = 10;

    public TimeSpan Window { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan Cooldown { get; set; } = TimeSpan.FromHours(1);

    public DateTime? SnoozedUntilUtc { get; set; }

    public DateTime? LastFiredUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }
}
