using Exceptionless.Core.Models;
using Exceptionless.Core.Seed;
using Xunit;

namespace Exceptionless.Tests.Seed;

public sealed class PredefinedSavedViewContentHasherTests
{
    [Fact]
    public void GetContentHash_ConfigurationDiffersOnlyBySpacesAndDictionaryInsertionOrder_ReturnsSameHash()
    {
        // Arrange
        var original = new SavedView
        {
            Name = "Logs",
            Slug = "logs",
            ViewType = "events",
            Filter = "type:log (status:open OR status:regressed)",
            FilterDefinitions = """[{"type":"status","value":"open"}]""",
            Columns = new Dictionary<string, bool>
            {
                ["date"] = true,
                ["summary"] = false
            },
            ColumnOrder = ["summary", "date"]
        };
        var reformatted = original with
        {
            Filter = "type:log(status:openORstatus:regressed)",
            FilterDefinitions = """[{"type":"status", "value":"open"}]""",
            Columns = new Dictionary<string, bool>
            {
                ["summary"] = false,
                ["date"] = true
            }
        };

        // Act
        string originalHash = PredefinedSavedViewContentHasher.GetContentHash(original);
        string reformattedHash = PredefinedSavedViewContentHasher.GetContentHash(reformatted);

        // Assert
        Assert.Equal(originalHash, reformattedHash);
    }
}
