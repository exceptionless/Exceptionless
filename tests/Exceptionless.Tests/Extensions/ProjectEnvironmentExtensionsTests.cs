using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Xunit;

namespace Exceptionless.Tests.Extensions;

public class ProjectEnvironmentExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("production")]
    [InlineData("Staging")]
    [InlineData("qa-west")]
    public void Settings_WithDefaultProject_DiscardNothingAndNotifyEverything(string? environment)
    {
        // Arrange
        var project = new Project();

        // Act
        bool discarded = project.ShouldDiscardEnvironment(environment);
        bool notifies = project.ShouldNotifyForEnvironment(environment);

        // Assert
        Assert.False(discarded);
        Assert.True(notifies);
        Assert.Equal(environment, project.GetEnvironmentDisplayName(environment));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("production")]
    [InlineData("PRODUCTION")]
    public void FindEnvironment_WithDifferentCasing_MatchesAndKeepsListedCasing(string eventEnvironment)
    {
        // Arrange
        var project = new Project { Environments = [new ProjectEnvironment { Name = "Production" }] };

        // Act
        var listed = project.FindEnvironment(eventEnvironment);

        // Assert
        Assert.NotNull(listed);
        Assert.Equal("Production", project.GetEnvironmentDisplayName(eventEnvironment));
    }

    [Fact]
    public void ShouldDiscardEnvironment_WithDiscardUnlisted_DiscardsOnlyUnlistedNamesCaseInsensitively()
    {
        // Arrange
        var project = new Project
        {
            DiscardUnlistedEnvironments = true,
            Environments = [new ProjectEnvironment { Name = "Production" }, new ProjectEnvironment { Name = "staging" }]
        };

        // Act & Assert
        Assert.False(project.ShouldDiscardEnvironment("PRODUCTION"));
        Assert.False(project.ShouldDiscardEnvironment("Staging"));
        Assert.True(project.ShouldDiscardEnvironment("prodution"));
        Assert.True(project.ShouldDiscardEnvironment("dev"));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void ShouldDiscardEnvironment_WithoutEnvironment_DiscardsOnlyWhenBothSettingsAreOn(bool discardUnlisted, bool discardWithout, bool expected)
    {
        // Arrange
        var project = new Project
        {
            DiscardUnlistedEnvironments = discardUnlisted,
            DiscardEventsWithoutEnvironment = discardWithout,
            Environments = [new ProjectEnvironment { Name = "production" }]
        };

        // Act
        bool discarded = project.ShouldDiscardEnvironment(null);

        // Assert
        Assert.Equal(expected, discarded);
    }

    [Theory]
    [InlineData("production", false, true)]
    [InlineData("PRODUCTION", false, true)]
    [InlineData("dev", true, false)]
    [InlineData("DEV", true, false)]
    [InlineData("qa", true, true)]
    [InlineData("qa", false, false)]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    public void ShouldNotifyForEnvironment_ListedUsesItsOwnSettingAndOthersUseNotifyOtherEnvironments(string? environment, bool notifyOthers, bool expected)
    {
        // Arrange
        var project = new Project
        {
            NotifyOtherEnvironments = notifyOthers,
            Environments =
            [
                new ProjectEnvironment { Name = "Production", Notify = true },
                new ProjectEnvironment { Name = "dev", Notify = false }
            ]
        };

        // Act
        bool notifies = project.ShouldNotifyForEnvironment(environment);

        // Assert
        Assert.Equal(expected, notifies);
    }
}
