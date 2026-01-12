using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Validation;
using Xunit;

namespace Exceptionless.Tests.Validation;

public sealed class TokenValidatorTests : TestWithServices
{
    private const string ValidObjectId = "123456789012345678901234";
    private readonly TokenValidator _validator;

    public TokenValidatorTests(ITestOutputHelper output) : base(output)
    {
        _validator = new TokenValidator();
    }

    private Token CreateValidToken()
    {
        return new Token
        {
            Id = SampleDataService.TEST_API_KEY,
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Type = TokenType.Access,
            IsDisabled = false,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    [Theory]
    [InlineData("test-api-key")]
    public void Validate_WhenIdIsValid_ReturnsSuccess(string id)
    {
        // Arrange
        var token = CreateValidToken();
        token.Id = id;

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenIdIsEmpty_ReturnsError(string? id)
    {
        // Arrange
        var token = CreateValidToken();
        token.Id = id!;

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Token.Id)));
    }

    [Fact]
    public void Validate_WhenOrganizationIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var token = CreateValidToken();
        token.OrganizationId = ValidObjectId;

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    public void Validate_WhenOrganizationIdIsInvalidObjectId_ReturnsError(string organizationId)
    {
        // Arrange
        var token = CreateValidToken();
        token.OrganizationId = organizationId;

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Token.OrganizationId)));
    }

    [Fact]
    public void Validate_WhenOrganizationIdIsEmptyAndProjectIdIsSet_ReturnsError()
    {
        // Arrange
        var token = CreateValidToken();
        token.OrganizationId = "";
        token.ProjectId = ValidObjectId;

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Token.OrganizationId)));
    }

    [Fact]
    public void Validate_WhenOrganizationIdIsEmptyAndUserIdIsEmpty_ReturnsError()
    {
        // Arrange
        var token = CreateValidToken();
        token.OrganizationId = "";
        token.UserId = "";

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Token.OrganizationId)));
    }

    [Fact]
    public void Validate_WhenOrganizationIdIsEmptyButUserIdIsSet_ReturnsSuccess()
    {
        // Arrange
        var token = CreateValidToken();
        token.OrganizationId = "";
        token.UserId = ValidObjectId;
        token.ProjectId = "";

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenProjectIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var token = CreateValidToken();
        token.ProjectId = ValidObjectId;

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    public void Validate_WhenProjectIdIsInvalidObjectId_ReturnsError(string projectId)
    {
        // Arrange
        var token = CreateValidToken();
        token.ProjectId = projectId;

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Token.ProjectId)));
    }

    [Fact]
    public void Validate_WhenProjectIdIsEmpty_ReturnsSuccess()
    {
        // Arrange
        var token = CreateValidToken();
        token.ProjectId = "";

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenDefaultProjectIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var token = CreateValidToken();
        token.DefaultProjectId = ValidObjectId;

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    public void Validate_WhenDefaultProjectIdIsInvalidObjectId_ReturnsError(string defaultProjectId)
    {
        // Arrange
        var token = CreateValidToken();
        token.DefaultProjectId = defaultProjectId;

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Token.DefaultProjectId)));
    }

    [Fact]
    public void Validate_WhenDefaultProjectIdIsSetAndProjectIdIsDefined_ReturnsError()
    {
        // Arrange
        var token = CreateValidToken();
        token.ProjectId = ValidObjectId;
        token.DefaultProjectId = ValidObjectId;

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Token.DefaultProjectId)));
    }

    [Fact]
    public void Validate_WhenUserIdIsSetAndProjectIdIsDefined_ReturnsError()
    {
        // Arrange
        var token = CreateValidToken();
        token.ProjectId = ValidObjectId;
        token.UserId = ValidObjectId;

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Token.UserId)));
    }

    [Fact]
    public void Validate_WhenCreatedUtcIsDefault_ReturnsError()
    {
        // Arrange
        var token = CreateValidToken();
        token.CreatedUtc = default;

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Token.CreatedUtc)));
    }

    [Fact]
    public void Validate_WhenUpdatedUtcIsDefault_ReturnsError()
    {
        // Arrange
        var token = CreateValidToken();
        token.UpdatedUtc = default;

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Token.UpdatedUtc)));
    }

    [Theory]
    [InlineData(TokenType.Access, false)]
    [InlineData(TokenType.Access, true)]
    [InlineData(TokenType.Authentication, false)]
    public void Validate_WhenTokenTypeAndDisabledStateAreValid_ReturnsSuccess(TokenType type, bool isDisabled)
    {
        // Arrange
        var token = new Token
        {
            Id = SampleDataService.TEST_API_KEY,
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Type = type,
            IsDisabled = isDisabled,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        // Act
        var result = _validator.Validate(token);

        // Assert
        if (!result.IsValid)
            _logger.LogInformation(result.ToString());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenAuthenticationTokenIsDisabled_ReturnsError()
    {
        // Arrange
        var token = new Token
        {
            Id = SampleDataService.TEST_API_KEY,
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Type = TokenType.Authentication,
            IsDisabled = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WhenTokenIsValid_ReturnsSuccess()
    {
        // Arrange
        var token = CreateValidToken();

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenTokenIsValid_ReturnsSuccess()
    {
        // Arrange
        var token = CreateValidToken();

        // Act
        var result = await _validator.ValidateAsync(token, TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
    }
}
