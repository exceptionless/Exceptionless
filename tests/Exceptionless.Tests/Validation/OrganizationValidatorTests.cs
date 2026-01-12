using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Validation;
using Xunit;

namespace Exceptionless.Tests.Validation;

public sealed class OrganizationValidatorTests : TestWithServices
{
    private const string ValidObjectId = "123456789012345678901234";
    private readonly OrganizationValidator _validator;
    private readonly BillingPlans _plans;

    public OrganizationValidatorTests(ITestOutputHelper output) : base(output)
    {
        _plans = GetService<BillingPlans>();
        _validator = new OrganizationValidator(_plans);
    }

    private Organization CreateValidOrganization()
    {
        return new Organization
        {
            Id = SampleDataService.TEST_ORG_ID,
            Name = "Test Organization",
            PlanId = _plans.FreePlan.Id,
            PlanName = _plans.FreePlan.Name,
            PlanDescription = _plans.FreePlan.Description
        };
    }

    [Fact]
    public void Validate_WhenNameIsValid_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.Name = "Valid Name";

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenNameIsEmpty_ReturnsError(string? name)
    {
        // Arrange
        var org = CreateValidOrganization();
        org.Name = name!;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Organization.Name)));
    }

    [Fact]
    public void Validate_WhenPlanIdIsValid_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = "Valid-Plan-Id";

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenPlanIdIsEmpty_ReturnsError(string? planId)
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = planId!;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Organization.PlanId)));
    }

    [Fact]
    public void Validate_WhenHasPremiumFeaturesOnFreePlan_ReturnsError()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = _plans.FreePlan.Id;
        org.HasPremiumFeatures = true;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Organization.HasPremiumFeatures)));
    }

    [Fact]
    public void Validate_WhenNoPremiumFeaturesOnFreePlan_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = _plans.FreePlan.Id;
        org.HasPremiumFeatures = false;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenHasPremiumFeaturesOnPaidPlan_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = _plans.SmallPlan.Id;
        org.HasPremiumFeatures = true;
        org.BillingPrice = 0;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenBillingPriceGreaterThanZeroAndStripeCustomerIdIsValid_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = _plans.SmallPlan.Id;
        org.BillingPrice = 10.0m;
        org.StripeCustomerId = "cust_123";
        org.CardLast4 = "4242";
        org.SubscribeDate = DateTime.UtcNow;
        org.BillingChangeDate = DateTime.UtcNow;
        org.BillingChangedByUserId = ValidObjectId;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenBillingPriceGreaterThanZeroAndStripeCustomerIdIsEmpty_ReturnsError(string? stripeCustomerId)
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = _plans.SmallPlan.Id;
        org.BillingPrice = 10.0m;
        org.StripeCustomerId = stripeCustomerId;
        org.CardLast4 = "4242";
        org.SubscribeDate = DateTime.UtcNow;
        org.BillingChangeDate = DateTime.UtcNow;
        org.BillingChangedByUserId = ValidObjectId;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Organization.StripeCustomerId)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenBillingPriceGreaterThanZeroAndCardLast4IsEmpty_ReturnsError(string? cardLast4)
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = _plans.SmallPlan.Id;
        org.BillingPrice = 10.0m;
        org.StripeCustomerId = "cust_123";
        org.CardLast4 = cardLast4;
        org.SubscribeDate = DateTime.UtcNow;
        org.BillingChangeDate = DateTime.UtcNow;
        org.BillingChangedByUserId = ValidObjectId;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Organization.CardLast4)));
    }

    [Fact]
    public void Validate_WhenBillingPriceGreaterThanZeroAndSubscribeDateIsNull_ReturnsError()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = _plans.SmallPlan.Id;
        org.BillingPrice = 10.0m;
        org.StripeCustomerId = "cust_123";
        org.CardLast4 = "4242";
        org.SubscribeDate = null;
        org.BillingChangeDate = DateTime.UtcNow;
        org.BillingChangedByUserId = ValidObjectId;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Organization.SubscribeDate)));
    }

    [Fact]
    public void Validate_WhenBillingPriceGreaterThanZeroAndBillingChangeDateIsMinValue_ReturnsError()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = _plans.SmallPlan.Id;
        org.BillingPrice = 10.0m;
        org.StripeCustomerId = "cust_123";
        org.CardLast4 = "4242";
        org.SubscribeDate = DateTime.UtcNow;
        org.BillingChangeDate = DateTime.MinValue;
        org.BillingChangedByUserId = ValidObjectId;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Organization.BillingChangeDate)));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenBillingPriceGreaterThanZeroAndBillingChangedByUserIdIsInvalid_ReturnsError(string? userId)
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = _plans.SmallPlan.Id;
        org.BillingPrice = 10.0m;
        org.StripeCustomerId = "cust_123";
        org.CardLast4 = "4242";
        org.SubscribeDate = DateTime.UtcNow;
        org.BillingChangeDate = DateTime.UtcNow;
        org.BillingChangedByUserId = userId;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Organization.BillingChangedByUserId)));
    }

    [Theory]
    [InlineData(SuspensionCode.Billing)]
    [InlineData(SuspensionCode.Abuse)]
    [InlineData(SuspensionCode.Overage)]
    public void Validate_WhenSuspendedWithValidSuspensionCode_ReturnsSuccess(SuspensionCode suspensionCode)
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = suspensionCode;
        org.SuspensionDate = DateTime.UtcNow;
        org.SuspendedByUserId = ValidObjectId;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenSuspendedAndSuspensionCodeIsNull_ReturnsError()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = null;
        org.SuspensionDate = DateTime.UtcNow;
        org.SuspendedByUserId = ValidObjectId;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Organization.SuspensionCode)));
    }

    [Fact]
    public void Validate_WhenNotSuspendedAndSuspensionCodeIsSet_ReturnsError()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = false;
        org.SuspensionCode = SuspensionCode.Billing;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Organization.SuspensionCode)));
    }

    [Fact]
    public void Validate_WhenSuspendedAndSuspensionDateIsNull_ReturnsError()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = SuspensionCode.Billing;
        org.SuspensionDate = null;
        org.SuspendedByUserId = ValidObjectId;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Organization.SuspensionDate)));
    }

    [Fact]
    public void Validate_WhenNotSuspendedAndSuspensionDateIsSet_ReturnsError()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = false;
        org.SuspensionDate = DateTime.UtcNow;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Organization.SuspensionDate)));
    }

    [Fact]
    public void Validate_WhenSuspendedAndSuspendedByUserIdIsValid_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = SuspensionCode.Billing;
        org.SuspensionDate = DateTime.UtcNow;
        org.SuspendedByUserId = ValidObjectId;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenSuspendedAndSuspendedByUserIdIsEmpty_ReturnsError(string? userId)
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = SuspensionCode.Billing;
        org.SuspensionDate = DateTime.UtcNow;
        org.SuspendedByUserId = userId;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Organization.SuspendedByUserId)));
    }

    [Fact]
    public void Validate_WhenNotSuspendedAndSuspendedByUserIdIsSet_ReturnsError()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = false;
        org.SuspendedByUserId = ValidObjectId;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Organization.SuspendedByUserId)));
    }

    [Fact]
    public void Validate_WhenSuspendedWithOtherCodeAndNotesIsSet_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = SuspensionCode.Other;
        org.SuspensionDate = DateTime.UtcNow;
        org.SuspendedByUserId = ValidObjectId;
        org.SuspensionNotes = "Suspended for suspicious activity";

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenSuspendedWithOtherCodeAndNotesIsEmpty_ReturnsError(string? notes)
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = SuspensionCode.Other;
        org.SuspensionDate = DateTime.UtcNow;
        org.SuspendedByUserId = ValidObjectId;
        org.SuspensionNotes = notes;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Organization.SuspensionNotes)));
    }

    [Fact]
    public void Validate_WhenSuspendedWithNonOtherCodeAndNotesIsNull_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = SuspensionCode.Billing;
        org.SuspensionDate = DateTime.UtcNow;
        org.SuspendedByUserId = ValidObjectId;
        org.SuspensionNotes = null;

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenOrganizationIsValid_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();

        // Act
        var result = _validator.Validate(org);

        // Assert
        Assert.True(result.IsValid);
    }
}
