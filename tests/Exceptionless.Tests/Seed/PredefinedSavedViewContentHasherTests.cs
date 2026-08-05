using Exceptionless.Core.Models;
using Exceptionless.Core.Seed;
using Xunit;

namespace Exceptionless.Tests.Seed;

public sealed class PredefinedSavedViewContentHasherTests
{
    [Fact]
    public void GetContentHash_FilterDefinitionsDifferOnlyByFormattingAndDictionaryInsertionOrder_ReturnsSameHash()
    {
        // Arrange
        var original = new SavedView
        {
            Name = "Logs",
            Slug = "logs",
            ViewType = "events",
            Filter = "type:log (status:open OR status:regressed)",
            FilterDefinitions = """[{"type":"status","value":"open"}]""",
            Columns = new Dictionary<string, SavedViewColumnSettings>
            {
                ["date"] = new() { Position = 1, Visible = true },
                ["summary"] = new() { Position = 0, Visible = false }
            }
        };
        var reformatted = original with
        {
            Filter = "type:log (status:open OR status:regressed)",
            FilterDefinitions = """[{"type":"status", "value":"open"}]""",
            Columns = new Dictionary<string, SavedViewColumnSettings>
            {
                ["summary"] = new() { Position = 0, Visible = false },
                ["date"] = new() { Position = 1, Visible = true }
            }
        };

        // Act
        string originalHash = PredefinedSavedViewContentHasher.GetContentHash(original);
        string reformattedHash = PredefinedSavedViewContentHasher.GetContentHash(reformatted);

        // Assert
        Assert.Equal(originalHash, reformattedHash);
    }

    [Fact]
    public void GetContentHash_StringFieldDiffersOnlyBySpaces_ReturnsDifferentHash()
    {
        // Arrange
        var withSpaces = new SavedView
        {
            Name = "Open Issues",
            Slug = "open-issues",
            ViewType = "events"
        };
        var withoutSpaces = withSpaces with { Name = "OpenIssues" };

        // Act
        string withSpacesHash = PredefinedSavedViewContentHasher.GetContentHash(withSpaces);
        string withoutSpacesHash = PredefinedSavedViewContentHasher.GetContentHash(withoutSpaces);

        // Assert
        Assert.NotEqual(withSpacesHash, withoutSpacesHash);
    }

    [Fact]
    public void GetContentHash_SameView_ReturnsSameHash()
    {
        // Arrange
        var savedView = new SavedView
        {
            Name = "Open Issues",
            Slug = "open-issues",
            ViewType = "events"
        };

        // Act
        string firstHash = PredefinedSavedViewContentHasher.GetContentHash(savedView);
        string secondHash = PredefinedSavedViewContentHasher.GetContentHash(savedView);

        // Assert
        Assert.Equal(firstHash, secondHash);
    }

    [Fact]
    public void GetContentHash_ColumnInsertionOrderDiffers_ReturnsSameHash()
    {
        // Arrange
        var original = new SavedView
        {
            Name = "Logs",
            Slug = "logs",
            ViewType = "events",
            Columns = new Dictionary<string, SavedViewColumnSettings>
            {
                ["project"] = new() { Position = 1, Visible = true, Width = 240 },
                ["summary"] = new() { Position = 0, Visible = true, Width = 420 }
            }
        };
        var reordered = original with
        {
            Columns = new Dictionary<string, SavedViewColumnSettings>
            {
                ["summary"] = new() { Position = 0, Visible = true, Width = 420 },
                ["project"] = new() { Position = 1, Visible = true, Width = 240 }
            }
        };

        // Act
        string originalHash = PredefinedSavedViewContentHasher.GetContentHash(original);
        string reorderedHash = PredefinedSavedViewContentHasher.GetContentHash(reordered);

        // Assert
        Assert.Equal(originalHash, reorderedHash);
    }
}
