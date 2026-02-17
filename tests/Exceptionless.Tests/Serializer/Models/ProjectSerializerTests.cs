using Exceptionless.Core.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests Project serialization through ITextSerializer.
/// Critical: Project.Configuration contains SettingsDictionary which must
/// survive round-trip through the cache serializer (STJ).
/// </summary>
public class ProjectSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public ProjectSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_RoundTrip_PreservesConfigurationSettings()
    {
        // Arrange — simulates what the cache serializer does in ProjectRepository.GetConfigAsync
        var project = new Project
        {
            Id = "test-project-id",
            OrganizationId = "test-org-id",
            Name = "Test Project"
        };
        project.Configuration.Version = 10;
        project.Configuration.Settings["IncludeConditionalData"] = "true";
        project.Configuration.Settings["DataExclusions"] = "password,secret";

        // Act
        string json = _serializer.SerializeToString(project);
        var deserialized = _serializer.Deserialize<Project>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("test-project-id", deserialized.Id);
        Assert.Equal(10, deserialized.Configuration.Version);
        Assert.Equal(2, deserialized.Configuration.Settings.Count);
        Assert.True(deserialized.Configuration.Settings.GetBoolean("IncludeConditionalData"));
        Assert.Equal("password,secret", deserialized.Configuration.Settings.GetString("DataExclusions"));
    }

    [Fact]
    public void Deserialize_RoundTrip_PreservesBasicProperties()
    {
        // Arrange
        var project = new Project
        {
            Id = "proj1",
            OrganizationId = "org1",
            Name = "My Project",
            NextSummaryEndOfDayTicks = 637500000000000000,
            IsConfigured = true
        };

        // Act
        string json = _serializer.SerializeToString(project);
        var deserialized = _serializer.Deserialize<Project>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("proj1", deserialized.Id);
        Assert.Equal("org1", deserialized.OrganizationId);
        Assert.Equal("My Project", deserialized.Name);
        Assert.True(deserialized.IsConfigured);
    }
}
