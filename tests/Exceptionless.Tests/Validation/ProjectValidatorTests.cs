using Exceptionless.Core.Models;
using Exceptionless.Core.Validation;
using Xunit;

namespace Exceptionless.Tests.Validation;

public sealed class ProjectValidatorTests : TestWithServices
{
    private const string ValidObjectId = "123456789012345678901234";
    private readonly MiniValidationValidator _validator;

    public ProjectValidatorTests(ITestOutputHelper output) : base(output)
    {
        _validator = GetService<MiniValidationValidator>();
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
    public async Task Validate_WhenEnvironmentsAreValid_ReturnsSuccess()
    {
        // Arrange
        var project = CreateValidProject();
        project.Environments = [new ProjectEnvironment { Name = "Production" }, new ProjectEnvironment { Name = "qa-west.2", Notify = false }];

        // Act
        var (isValid, _) = await _validator.ValidateAsync(project);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" production")]
    [InlineData("production ")]
    [InlineData("prod\tuction")]
    [InlineData("prod\nuction")]
    [InlineData("Staging West")]
    [InlineData("prod/eu")]
    [InlineData("-prod")]
    public async Task Validate_WhenEnvironmentNameIsInvalid_ReturnsError(string name)
    {
        // Arrange
        var project = CreateValidProject();
        project.Environments = [new ProjectEnvironment { Name = name }];

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(project);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Project.Environments)));
    }

    [Fact]
    public async Task Validate_WhenEnvironmentNameIsTooLong_ReturnsError()
    {
        // Arrange
        var project = CreateValidProject();
        project.Environments = [new ProjectEnvironment { Name = new string('a', 65) }];

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(project);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Project.Environments)));
    }

    [Fact]
    public async Task Validate_WhenEnvironmentNamesDifferOnlyByCase_ReturnsError()
    {
        // Arrange
        var project = CreateValidProject();
        project.Environments = [new ProjectEnvironment { Name = "Production" }, new ProjectEnvironment { Name = "PRODUCTION" }];

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(project);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Project.Environments)));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public async Task Validate_WhenEnvironmentCountIsAtOrOverTheLimit_ReturnsExpectedResult(int count, bool expected)
    {
        // Arrange
        var project = CreateValidProject();
        project.Environments = Enumerable.Range(0, count).Select(i => new ProjectEnvironment { Name = $"env-{i}" }).ToList();

        // Act
        var (isValid, _) = await _validator.ValidateAsync(project);

        // Assert
        Assert.Equal(expected, isValid);
    }

    [Fact]
    public async Task Validate_WhenOrganizationIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var project = CreateValidProject();
        project.OrganizationId = ValidObjectId;

        // Act
        var (isValid, _) = await _validator.ValidateAsync(project);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenOrganizationIdIsInvalid_ReturnsError(string? organizationId)
    {
        // Arrange
        var project = CreateValidProject();
        project.OrganizationId = organizationId!;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(project);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Project.OrganizationId)));
    }

    [Theory]
    [InlineData("My Project")]
    public async Task Validate_WhenNameIsValid_ReturnsSuccess(string name)
    {
        // Arrange
        var project = CreateValidProject();
        project.Name = name;

        // Act
        var (isValid, _) = await _validator.ValidateAsync(project);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenNameIsEmpty_ReturnsError(string? name)
    {
        // Arrange
        var project = CreateValidProject();
        project.Name = name!;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(project);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Project.Name)));
    }

    [Theory]
    [InlineData(638000000000000000)]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task Validate_WhenNextSummaryEndOfDayTicksIsNonZero_ReturnsSuccess(long ticks)
    {
        // Arrange
        var project = CreateValidProject();
        project.NextSummaryEndOfDayTicks = ticks;

        // Act
        var (isValid, _) = await _validator.ValidateAsync(project);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task Validate_WhenNextSummaryEndOfDayTicksIsZero_ReturnsError()
    {
        // Arrange
        var project = CreateValidProject();
        project.NextSummaryEndOfDayTicks = 0;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(project);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Project.NextSummaryEndOfDayTicks)));
    }

    [Fact]
    public async Task Validate_WhenProjectIsValid_ReturnsSuccess()
    {
        // Arrange
        var project = CreateValidProject();

        // Act
        var (isValid, _) = await _validator.ValidateAsync(project);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenProjectIsValid_ReturnsSuccess()
    {
        // Arrange
        var project = CreateValidProject();

        // Act
        var (isValid, _) = await _validator.ValidateAsync(project);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task Validate_WhenMultipleFieldsAreInvalid_ReturnsMultipleErrors()
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
        var (isValid, errors) = await _validator.ValidateAsync(project);

        // Assert — MiniValidation short-circuits IValidatableObject when attribute validation fails,
        // so NextSummaryEndOfDayTicks error (from Validate()) won't appear. We get 2 attribute errors.
        Assert.False(isValid);
        Assert.True(errors.Count >= 2);
    }
}
