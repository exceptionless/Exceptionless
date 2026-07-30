using Exceptionless.Core.Models;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models;
using Xunit;

namespace Exceptionless.Tests.Extensions;

public class ViewOrganizationExtensionsTests
{
    private readonly ProxyTimeProvider _timeProvider = new();

    [Fact]
    public void EnsureUsage_ActiveBonus_AppliesBonusToMissingMonths()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new ViewOrganization
        {
            CreatedUtc = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxEventsPerMonth = 15_000,
            BonusEventsPerMonth = 5_000,
            BonusExpiration = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        organization.EnsureUsage(_timeProvider);

        // Assert
        Assert.Equal(3, organization.Usage.Count);
        Assert.All(organization.Usage, usage => Assert.Equal(20_000, usage.Limit));
    }

    [Fact]
    public void EnsureUsage_ActiveBonus_DoesNotSubtractBonusFromOutgoingPlanAnchor()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new ViewOrganization
        {
            CreatedUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxEventsPerMonth = 15_000,
            BonusEventsPerMonth = 5_000,
            BonusExpiration = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
            BillingChangeDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Usage =
            [
                new UsageInfo { Date = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 75_000 },
                new UsageInfo { Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 20_000 }
            ]
        };

        // Act
        organization.EnsureUsage(_timeProvider);

        // Assert
        Assert.Equal(75_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
        Assert.Equal(75_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
    }

    [Fact]
    public void EnsureUsage_ActiveBonus_PreservesEqualValuedOutgoingPlanAnchor()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new ViewOrganization
        {
            CreatedUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxEventsPerMonth = 15_000,
            BonusEventsPerMonth = 60_000,
            BonusExpiration = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
            BillingChangeDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Usage =
            [
                new UsageInfo { Date = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 75_000 },
                new UsageInfo { Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 75_000 }
            ]
        };

        // Act
        organization.EnsureUsage(_timeProvider);

        // Assert
        Assert.Equal(75_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
        Assert.Equal(75_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
    }

    [Fact]
    public void EnsureUsage_ExpiredBonus_DistinguishesPriorBonusFromFuturePlan()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new ViewOrganization
        {
            CreatedUtc = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxEventsPerMonth = 75_000,
            BonusEventsPerMonth = 60_000,
            BonusExpiration = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            BillingChangeDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Usage =
            [
                new UsageInfo { Date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 75_000 },
                new UsageInfo { Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 75_000 }
            ]
        };

        // Act
        organization.EnsureUsage(_timeProvider);

        // Assert
        Assert.Equal(15_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
    }

    [Fact]
    public void EnsureUsage_ExpiredBonus_DoesNotBackfillFromFuturePlan()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new ViewOrganization
        {
            CreatedUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxEventsPerMonth = 75_000,
            BonusEventsPerMonth = 5_000,
            BonusExpiration = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            BillingChangeDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Usage =
            [
                new UsageInfo { Date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 20_000 },
                new UsageInfo { Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 75_000 }
            ]
        };

        // Act
        organization.EnsureUsage(_timeProvider);

        // Assert
        Assert.Equal(15_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
        Assert.Equal(15_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
    }

    [Fact]
    public void EnsureUsage_ExpiredBonus_DoesNotCarryBonusIntoLaterMonths()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new ViewOrganization
        {
            CreatedUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxEventsPerMonth = 15_000,
            BonusEventsPerMonth = 5_000,
            BonusExpiration = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            Usage =
            [
                new UsageInfo { Date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 20_000 }
            ]
        };

        // Act
        organization.EnsureUsage(_timeProvider);

        // Assert
        Assert.Equal(15_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
        Assert.Equal(20_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
        Assert.Equal(15_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
        Assert.Equal(15_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
    }

    [Fact]
    public void EnsureUsage_ExpiredBonus_DoesNotCarryExpirationMonthBonusForward()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new ViewOrganization
        {
            CreatedUtc = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxEventsPerMonth = 15_000,
            BonusEventsPerMonth = 5_000,
            BonusExpiration = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            Usage =
            [
                new UsageInfo { Date = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 20_000 }
            ]
        };

        // Act
        organization.EnsureUsage(_timeProvider);

        // Assert
        Assert.Equal(20_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
        Assert.Equal(15_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
        Assert.Equal(15_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
    }

    [Fact]
    public void EnsureUsage_ExpiredBonus_DoesNotSubtractExpirationMonthBaseAfterPlanChange()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new ViewOrganization
        {
            CreatedUtc = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxEventsPerMonth = 75_000,
            BonusEventsPerMonth = 5_000,
            BonusExpiration = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            BillingChangeDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            Usage =
            [
                new UsageInfo { Date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 20_000 },
                new UsageInfo { Date = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 15_000 },
                new UsageInfo { Date = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 75_000 }
            ]
        };

        // Act
        organization.EnsureUsage(_timeProvider);

        // Assert
        Assert.Equal(15_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
    }

    [Fact]
    public void EnsureUsage_ExpiredBonusBeforeWindow_DoesNotCarryBonusIntoWindow()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2027, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new ViewOrganization
        {
            CreatedUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxEventsPerMonth = 15_000,
            BonusEventsPerMonth = 5_000,
            BonusExpiration = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            Usage =
            [
                new UsageInfo { Date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 20_000 }
            ]
        };

        // Act
        organization.EnsureUsage(_timeProvider);

        // Assert
        Assert.Equal(12, organization.Usage.Count(u => u.Date >= new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.All(organization.Usage.Where(u => u.Date >= new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)),
            usage => Assert.Equal(15_000, usage.Limit));
    }

    [Fact]
    public void EnsureUsage_ExpiredBonusBeforeWindow_DoesNotSubtractBonusFromFallback()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2027, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new ViewOrganization
        {
            CreatedUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxEventsPerMonth = 75_000,
            BonusEventsPerMonth = 60_000,
            BonusExpiration = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            BillingChangeDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        organization.EnsureUsage(_timeProvider);

        // Assert
        Assert.Equal(12, organization.Usage.Count(u => u.Date >= new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.All(organization.Usage.Where(u => u.Date >= new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)),
            usage => Assert.Equal(75_000, usage.Limit));
    }

    [Fact]
    public void EnsureUsage_PreBonusLimit_DoesNotSubtractBonusAgain()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new ViewOrganization
        {
            CreatedUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxEventsPerMonth = 15_000,
            BonusEventsPerMonth = 5_000,
            BonusExpiration = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            Usage =
            [
                new UsageInfo { Date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 15_000 }
            ]
        };

        // Act
        organization.EnsureUsage(_timeProvider);

        // Assert
        Assert.All(organization.Usage, usage => Assert.Equal(15_000, usage.Limit));
    }

    [Fact]
    public void EnsureUsage_SparseHistory_CarriesKnownLimitsBetweenChanges()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new ViewOrganization
        {
            CreatedUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxEventsPerMonth = 250_000,
            Usage =
            [
                new UsageInfo { Date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 75_000 },
                new UsageInfo { Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 250_000 }
            ]
        };

        // Act
        organization.EnsureUsage(_timeProvider);

        // Assert
        Assert.Equal(75_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
        Assert.Equal(75_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
        Assert.Equal(250_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
    }

    [Fact]
    public void EnsureUsage_UnlimitedPlan_CarriesUnlimitedLimit()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new ViewOrganization
        {
            CreatedUtc = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxEventsPerMonth = -1
        };

        // Act
        organization.EnsureUsage(_timeProvider);

        // Assert
        Assert.Equal(3, organization.Usage.Count);
        Assert.All(organization.Usage, usage => Assert.Equal(-1, usage.Limit));
    }

    [Fact]
    public void EnsureUsage_UnsetLimit_ReplacesItWithKnownLimit()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new ViewOrganization
        {
            CreatedUtc = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxEventsPerMonth = 15_000,
            Usage =
            [
                new UsageInfo { Date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 15_000 },
                new UsageInfo { Date = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 0 }
            ]
        };

        // Act
        organization.EnsureUsage(_timeProvider);

        // Assert
        Assert.Equal(15_000, organization.Usage.Single(u => u.Date == new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
    }

    [Fact]
    public void GetUsage_ExistingUsage_PreservesKnownLimit()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var existingUsage = new UsageInfo
        {
            Date = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            Limit = 15_000
        };
        var organization = new ViewOrganization
        {
            MaxEventsPerMonth = 75_000,
            Usage = [existingUsage]
        };

        // Act
        var usage = organization.GetUsage(existingUsage.Date, _timeProvider);

        // Assert
        Assert.Same(existingUsage, usage);
        Assert.Equal(15_000, usage.Limit);
    }
}
