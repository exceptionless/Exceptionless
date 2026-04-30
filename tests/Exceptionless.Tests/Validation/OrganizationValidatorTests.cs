using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Validation;
using Xunit;

namespace Exceptionless.Tests.Validation;

public sealed class OrganizationValidatorTests : TestWithServices
{
    private const string ValidObjectId = "123456789012345678901234";
    private readonly MiniValidationValidator _validator;
    private readonly BillingPlans _plans;

    public OrganizationValidatorTests(ITestOutputHelper output) : base(output)
    {
        _plans = GetService<BillingPlans>();
        _validator = GetService<MiniValidationValidator>();
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
    public async Task Validate_WhenNameIsValid_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.Name = "Valid Name";

        // Act
        var (isValid, _) = await _validator.ValidateAsync(org);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenNameIsEmpty_ReturnsError(string? name)
    {
        // Arrange
        var org = CreateValidOrganization();
        org.Name = name!;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Organization.Name)));
    }

    [Fact]
    public async Task Validate_WhenPlanIdIsValid_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = "Valid-Plan-Id";

        // Act
        var (isValid, _) = await _validator.ValidateAsync(org);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenPlanIdIsEmpty_ReturnsError(string? planId)
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = planId!;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Organization.PlanId)));
    }

    [Fact]
    public async Task Validate_WhenHasPremiumFeaturesOnFreePlan_ReturnsError()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = _plans.FreePlan.Id;
        org.HasPremiumFeatures = true;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Organization.HasPremiumFeatures)));
    }

    [Fact]
    public async Task Validate_WhenNoPremiumFeaturesOnFreePlan_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = _plans.FreePlan.Id;
        org.HasPremiumFeatures = false;

        // Act
        var (isValid, _) = await _validator.ValidateAsync(org);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task Validate_WhenHasPremiumFeaturesOnPaidPlan_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.PlanId = _plans.SmallPlan.Id;
        org.HasPremiumFeatures = true;
        org.BillingPrice = 0;

        // Act
        var (isValid, _) = await _validator.ValidateAsync(org);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task Validate_WhenBillingPriceGreaterThanZeroAndStripeCustomerIdIsValid_ReturnsSuccess()
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
        var (isValid, _) = await _validator.ValidateAsync(org);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenBillingPriceGreaterThanZeroAndStripeCustomerIdIsEmpty_ReturnsError(string? stripeCustomerId)
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
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Organization.StripeCustomerId)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenBillingPriceGreaterThanZeroAndCardLast4IsEmpty_ReturnsError(string? cardLast4)
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
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Organization.CardLast4)));
    }

    [Fact]
    public async Task Validate_WhenBillingPriceGreaterThanZeroAndSubscribeDateIsNull_ReturnsError()
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
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Organization.SubscribeDate)));
    }

    [Fact]
    public async Task Validate_WhenBillingPriceGreaterThanZeroAndBillingChangeDateIsMinValue_ReturnsError()
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
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Organization.BillingChangeDate)));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenBillingPriceGreaterThanZeroAndBillingChangedByUserIdIsInvalid_ReturnsError(string? userId)
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
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Organization.BillingChangedByUserId)));
    }

    [Theory]
    [InlineData(SuspensionCode.Billing)]
    [InlineData(SuspensionCode.Abuse)]
    [InlineData(SuspensionCode.Overage)]
    public async Task Validate_WhenSuspendedWithValidSuspensionCode_ReturnsSuccess(SuspensionCode suspensionCode)
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = suspensionCode;
        org.SuspensionDate = DateTime.UtcNow;
        org.SuspendedByUserId = ValidObjectId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task Validate_WhenSuspendedAndSuspensionCodeIsNull_ReturnsError()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = null;
        org.SuspensionDate = DateTime.UtcNow;
        org.SuspendedByUserId = ValidObjectId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Organization.SuspensionCode)));
    }

    [Fact]
    public async Task Validate_WhenNotSuspendedAndSuspensionCodeIsSet_ReturnsError()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = false;
        org.SuspensionCode = SuspensionCode.Billing;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Organization.SuspensionCode)));
    }

    [Fact]
    public async Task Validate_WhenSuspendedAndSuspensionDateIsNull_ReturnsError()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = SuspensionCode.Billing;
        org.SuspensionDate = null;
        org.SuspendedByUserId = ValidObjectId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Organization.SuspensionDate)));
    }

    [Fact]
    public async Task Validate_WhenNotSuspendedAndSuspensionDateIsSet_ReturnsError()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = false;
        org.SuspensionDate = DateTime.UtcNow;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Organization.SuspensionDate)));
    }

    [Fact]
    public async Task Validate_WhenSuspendedAndSuspendedByUserIdIsValid_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = SuspensionCode.Billing;
        org.SuspensionDate = DateTime.UtcNow;
        org.SuspendedByUserId = ValidObjectId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenSuspendedAndSuspendedByUserIdIsEmpty_ReturnsError(string? userId)
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = SuspensionCode.Billing;
        org.SuspensionDate = DateTime.UtcNow;
        org.SuspendedByUserId = userId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Organization.SuspendedByUserId)));
    }

    [Fact]
    public async Task Validate_WhenNotSuspendedAndSuspendedByUserIdIsSet_ReturnsError()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = false;
        org.SuspendedByUserId = ValidObjectId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Organization.SuspendedByUserId)));
    }

    [Fact]
    public async Task Validate_WhenSuspendedWithOtherCodeAndNotesIsSet_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = SuspensionCode.Other;
        org.SuspensionDate = DateTime.UtcNow;
        org.SuspendedByUserId = ValidObjectId;
        org.SuspensionNotes = "Suspended for suspicious activity";

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenSuspendedWithOtherCodeAndNotesIsEmpty_ReturnsError(string? notes)
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = SuspensionCode.Other;
        org.SuspensionDate = DateTime.UtcNow;
        org.SuspendedByUserId = ValidObjectId;
        org.SuspensionNotes = notes;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Organization.SuspensionNotes)));
    }

    [Fact]
    public async Task Validate_WhenSuspendedWithNonOtherCodeAndNotesIsNull_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();
        org.IsSuspended = true;
        org.SuspensionCode = SuspensionCode.Billing;
        org.SuspensionDate = DateTime.UtcNow;
        org.SuspendedByUserId = ValidObjectId;
        org.SuspensionNotes = null;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task Validate_WhenOrganizationIsValid_ReturnsSuccess()
    {
        // Arrange
        var org = CreateValidOrganization();

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(org);

        // Assert
        Assert.True(isValid);
    }
}
