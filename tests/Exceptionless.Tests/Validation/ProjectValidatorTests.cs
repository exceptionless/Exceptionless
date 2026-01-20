using Exceptionless.Core.Models;
using Exceptionless.Core.Validation;
using Xunit;

namespace Exceptionless.Tests.Validation;

public sealed class ProjectValidatorTests : TestWithServices
{
    private const string ValidObjectId = "123456789012345678901234";
    private readonly ProjectValidator _validator;

    public ProjectValidatorTests(ITestOutputHelper output) : base(output)
    {
        _validator = new ProjectValidator();
    }

    private Project CreateValidProject()
    {
        return new Project
        {
            Id = ValidObjectId,
            OrganizationId = ValidObjectId,
            Name = "Test Project",
            NextSummaryEndOfDayTicks = DateTimeOffset.UtcNow.Ticks
        };
    }

    [Fact]
    public void Validate_WhenOrganizationIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var project = CreateValidProject();
        project.OrganizationId = ValidObjectId;

        // Act
        var result = _validator.Validate(project);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenOrganizationIdIsInvalid_ReturnsError(string? organizationId)
    {
        // Arrange
        var project = CreateValidProject();
        project.OrganizationId = organizationId!;

        // Act
        var result = _validator.Validate(project);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Project.OrganizationId)));
    }

    [Theory]
    [InlineData("My Project")]
    public void Validate_WhenNameIsValid_ReturnsSuccess(string name)
    {
        // Arrange
        var project = CreateValidProject();
        project.Name = name;

        // Act
        var result = _validator.Validate(project);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenNameIsEmpty_ReturnsError(string? name)
    {
        // Arrange
        var project = CreateValidProject();
        project.Name = name!;

        // Act
        var result = _validator.Validate(project);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Project.Name)));
    }

    [Theory]
    [InlineData(638000000000000000)]
    [InlineData(1)]
    [InlineData(-1)]
    public void Validate_WhenNextSummaryEndOfDayTicksIsNonZero_ReturnsSuccess(long ticks)
    {
        // Arrange
        var project = CreateValidProject();
        project.NextSummaryEndOfDayTicks = ticks;

        // Act
        var result = _validator.Validate(project);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenNextSummaryEndOfDayTicksIsZero_ReturnsError()
    {
        // Arrange
        var project = CreateValidProject();
        project.NextSummaryEndOfDayTicks = 0;

        // Act
        var result = _validator.Validate(project);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Project.NextSummaryEndOfDayTicks)));
    }

    [Fact]
    public void Validate_WhenProjectIsValid_ReturnsSuccess()
    {
        // Arrange
        var project = CreateValidProject();

        // Act
        var result = _validator.Validate(project);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenProjectIsValid_ReturnsSuccess()
    {
        // Arrange
        var project = CreateValidProject();

        // Act
        var result = await _validator.ValidateAsync(project, TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenMultipleFieldsAreInvalid_ReturnsMultipleErrors()
    {
        // Arrange
        var project = new Project
        {
            Id = ValidObjectId,
            OrganizationId = "invalid",
            Name = String.Empty,
            NextSummaryEndOfDayTicks = 0
        };

        // Act
        var result = _validator.Validate(project);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 3);
    }
}
