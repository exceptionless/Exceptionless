using Exceptionless.Core.Models;
using Exceptionless.Core.Validation;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Validation;

public sealed class StackValidatorTests : TestWithServices
{
    private const string ValidObjectId = "123456789012345678901234";
    private readonly StackValidator _validator;

    public StackValidatorTests(ITestOutputHelper output) : base(output)
    {
        _validator = new StackValidator();
    }

    private Stack CreateValidStack()
    {
        return new Stack
        {
            Id = ValidObjectId,
            OrganizationId = ValidObjectId,
            ProjectId = ValidObjectId,
            Type = Event.KnownTypes.Error,
            SignatureHash = "abc123",
            SignatureInfo = new SettingsDictionary { { "key", "value" } },
            Title = "Test Stack"
        };
    }

    [Fact]
    public void Validate_WhenIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var stack = CreateValidStack();
        stack.Id = ValidObjectId;

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("12345678901234567890123")]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenIdIsInvalid_ReturnsError(string? id)
    {
        // Arrange
        var stack = CreateValidStack();
        stack.Id = id!;

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Stack.Id)));
    }

    [Fact]
    public void Validate_WhenOrganizationIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var stack = CreateValidStack();
        stack.OrganizationId = ValidObjectId;

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("12345678901234567890123")]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenOrganizationIdIsInvalid_ReturnsError(string? organizationId)
    {
        // Arrange
        var stack = CreateValidStack();
        stack.OrganizationId = organizationId!;

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Stack.OrganizationId)));
    }

    [Fact]
    public void Validate_WhenProjectIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var stack = CreateValidStack();
        stack.ProjectId = ValidObjectId;

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("12345678901234567890123")]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenProjectIdIsInvalid_ReturnsError(string? projectId)
    {
        // Arrange
        var stack = CreateValidStack();
        stack.ProjectId = projectId!;

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Stack.ProjectId)));
    }

    [Fact]
    public void Validate_WhenTitleExceedsMaxLength_ReturnsError()
    {
        // Arrange
        var stack = CreateValidStack();
        stack.Title = new string('a', 1001);

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Stack.Title)));
    }

    [Fact]
    public void Validate_WhenTitleIsWithinMaxLength_ReturnsSuccess()
    {
        // Arrange
        var stack = CreateValidStack();
        stack.Title = new string('a', 1000);

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("error")]
    [InlineData("log")]
    [InlineData("a")]
    [InlineData(null)]
    public void Validate_WhenTypeIsValid_ReturnsSuccess(string? type)
    {
        // Arrange
        var stack = CreateValidStack();
        stack.Type = type!;

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenTypeIsEmpty_ReturnsError()
    {
        // Arrange
        var stack = CreateValidStack();
        stack.Type = "";

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Stack.Type)));
    }

    [Fact]
    public void Validate_WhenTypeExceedsMaxLength_ReturnsError()
    {
        // Arrange
        var stack = CreateValidStack();
        stack.Type = new string('a', 101);

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Stack.Type)));
    }

    [Fact]
    public void Validate_WhenTypeIsWithinMaxLength_ReturnsSuccess()
    {
        // Arrange
        var stack = CreateValidStack();
        stack.Type = new string('a', 100);

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.True(result.IsValid);
    }


    [Theory]
    [InlineData("valid-tag")]
    public void Validate_WhenTagIsValid_ReturnsSuccess(string tag)
    {
        // Arrange
        var stack = CreateValidStack();
        stack.Tags.Add(tag);

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenTagIsEmpty_ReturnsError(string? tag)
    {
        // Arrange
        var stack = CreateValidStack();
        stack.Tags.Add(tag);

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Stack.Tags)));
    }

    [Fact]
    public void Validate_WhenTagExceedsMaxLength_ReturnsError()
    {
        // Arrange
        var stack = CreateValidStack();
        stack.Tags.Add(new string('a', 256));

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Stack.Tags)));
    }

    [Fact]
    public void Validate_WhenTagIsWithinMaxLength_ReturnsSuccess()
    {
        // Arrange
        var stack = CreateValidStack();
        stack.Tags.Add(new string('a', 255));

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.True(result.IsValid);
    }


    [Theory]
    [InlineData("hash")]
    public void Validate_WhenSignatureHashIsValid_ReturnsSuccess(string signatureHash)
    {
        // Arrange
        var stack = CreateValidStack();
        stack.SignatureHash = signatureHash;

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenSignatureHashIsEmpty_ReturnsError(string? signatureHash)
    {
        // Arrange
        var stack = CreateValidStack();
        stack.SignatureHash = signatureHash!;

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Stack.SignatureHash)));
    }

    [Fact]
    public void Validate_WhenSignatureInfoIsNull_ReturnsError()
    {
        // Arrange
        var stack = CreateValidStack();
        stack.SignatureInfo = null!;

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Stack.SignatureInfo)));
    }

    [Fact]
    public void Validate_WhenStackIsValid_ReturnsSuccess()
    {
        // Arrange
        var stack = CreateValidStack();

        // Act
        var result = _validator.Validate(stack);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenStackIsValid_ReturnsSuccess()
    {
        // Arrange
        var stack = CreateValidStack();

        // Act
        var result = await _validator.ValidateAsync(stack);

        // Assert
        Assert.True(result.IsValid);
    }
}
