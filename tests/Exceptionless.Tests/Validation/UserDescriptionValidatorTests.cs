using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Validation;
using Xunit;

namespace Exceptionless.Tests.Validation;

public sealed class UserDescriptionValidatorTests : TestWithServices
{
    private readonly UserDescriptionValidator _validator;

    public UserDescriptionValidatorTests(ITestOutputHelper output) : base(output)
    {
        _validator = new UserDescriptionValidator();
    }

    private UserDescription CreateValidUserDescription()
    {
        return new UserDescription
        {
            EmailAddress = "test@localhost.com",
            Description = "This is a test description"
        };
    }

    [Theory]
    [InlineData("valid@localhost.com")]
    [InlineData("test.user@localhost.co.uk")]
    [InlineData("user+tag@localhost.com")]
    public void Validate_WhenEmailAddressIsValid_ReturnsSuccess(string email)
    {
        // Arrange
        var userDescription = CreateValidUserDescription();
        userDescription.EmailAddress = email;

        // Act
        var result = _validator.Validate(userDescription);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@nodomain.com")]
    public void Validate_WhenEmailAddressIsInvalid_ReturnsError(string email)
    {
        // Arrange
        var userDescription = CreateValidUserDescription();
        userDescription.EmailAddress = email;

        // Act
        var result = _validator.Validate(userDescription);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(UserDescription.EmailAddress)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenEmailAddressIsEmpty_ReturnsSuccess(string? email)
    {
        // Arrange
        var userDescription = CreateValidUserDescription();
        userDescription.EmailAddress = email;

        // Act
        var result = _validator.Validate(userDescription);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Valid description")]
    [InlineData("a")]
    public void Validate_WhenDescriptionIsValid_ReturnsSuccess(string description)
    {
        // Arrange
        var userDescription = CreateValidUserDescription();
        userDescription.Description = description;

        // Act
        var result = _validator.Validate(userDescription);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenDescriptionIsEmpty_ReturnsError(string? description)
    {
        // Arrange
        var userDescription = CreateValidUserDescription();
        userDescription.Description = description;

        // Act
        var result = _validator.Validate(userDescription);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(UserDescription.Description)));
    }

    [Fact]
    public void Validate_WhenUserDescriptionIsValid_ReturnsSuccess()
    {
        // Arrange
        var userDescription = CreateValidUserDescription();

        // Act
        var result = _validator.Validate(userDescription);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenOnlyDescriptionIsSet_ReturnsSuccess()
    {
        // Arrange
        var userDescription = new UserDescription
        {
            Description = "Just a description, no email"
        };

        // Act
        var result = _validator.Validate(userDescription);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenUserDescriptionIsValid_ReturnsSuccess()
    {
        // Arrange
        var userDescription = CreateValidUserDescription();

        // Act
        var result = await _validator.ValidateAsync(userDescription, TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
    }
}
