using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

public class OrganizationSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public OrganizationSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void RoundTrip_WithAllCoreProperties_PreservesValues()
    {
        // Arrange
        var organization = new Organization
        {
            Id = "550000000000000000000001",
            Name = "Acme Corp",
            StripeCustomerId = "cus_abc123",
            PlanId = "EX_MEDIUM",
            PlanName = "Medium",
            PlanDescription = "Medium plan",
            CardLast4 = "4242",
            BillingStatus = BillingStatus.Active,
            BillingPrice = 49.99m,
            MaxEventsPerMonth = 50000,
            RetentionDays = 30,
            HasPremiumFeatures = true,
            MaxUsers = 10,
            MaxProjects = 25,
            CreatedUtc = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            UpdatedUtc = new DateTime(2024, 6, 1, 8, 30, 0, DateTimeKind.Utc)
        };

        // Act
        string? json = _serializer.SerializeToString(organization);
        var result = _serializer.Deserialize<Organization>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("550000000000000000000001", result.Id);
        Assert.Equal("Acme Corp", result.Name);
        Assert.Equal("cus_abc123", result.StripeCustomerId);
        Assert.Equal("EX_MEDIUM", result.PlanId);
        Assert.Equal(BillingStatus.Active, result.BillingStatus);
        Assert.Equal(49.99m, result.BillingPrice);
        Assert.Equal(50000, result.MaxEventsPerMonth);
        Assert.Equal(30, result.RetentionDays);
        Assert.True(result.HasPremiumFeatures);
        Assert.Equal(10, result.MaxUsers);
    }

    [Fact]
    public void RoundTrip_WithInvites_PreservesCollection()
    {
        // Arrange
        var organization = new Organization
        {
            Id = "550000000000000000000002",
            Name = "Test Organization",
            PlanId = "EX_FREE",
            PlanName = "Free",
            PlanDescription = "Free plan",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
        organization.Invites.Add(new Invite
        {
            Token = "invite-token-123",
            EmailAddress = "new@example.com",
            DateAdded = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        // Act
        string? json = _serializer.SerializeToString(organization);
        var result = _serializer.Deserialize<Organization>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Invites);
        Assert.Equal("invite-token-123", result.Invites.First().Token);
        Assert.Equal("new@example.com", result.Invites.First().EmailAddress);
    }

    [Fact]
    public void RoundTrip_WithSuspension_PreservesValues()
    {
        // Arrange
        var organization = new Organization
        {
            Id = "550000000000000000000003",
            Name = "Suspended Organization",
            PlanId = "EX_FREE",
            PlanName = "Free",
            PlanDescription = "Free plan",
            IsSuspended = true,
            SuspensionCode = SuspensionCode.Billing,
            SuspensionNotes = "Payment failed",
            SuspensionDate = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            SuspendedByUserId = "660000000000000000000001",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        // Act
        string? json = _serializer.SerializeToString(organization);
        var result = _serializer.Deserialize<Organization>(json);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuspended);
        Assert.Equal(SuspensionCode.Billing, result.SuspensionCode);
        Assert.Equal("Payment failed", result.SuspensionNotes);
        Assert.Equal("660000000000000000000001", result.SuspendedByUserId);
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesCorrectly()
    {
        // Arrange
        /* language=json */
        const string json = """{"id":"550000000000000000000004","name":"Acme Industries","plan_id":"EX_SMALL","plan_name":"Small","plan_description":"Small plan","billing_status":1,"max_events_per_month":10000,"retention_days":7,"has_premium_features":false,"max_users":5,"max_projects":10,"created_utc":"2024-01-01T00:00:00Z","updated_utc":"2024-01-01T00:00:00Z"}""";

        // Act
        var result = _serializer.Deserialize<Organization>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("550000000000000000000004", result.Id);
        Assert.Equal("Acme Industries", result.Name);
        Assert.Equal("EX_SMALL", result.PlanId);
        Assert.Equal(10000, result.MaxEventsPerMonth);
    }
}
