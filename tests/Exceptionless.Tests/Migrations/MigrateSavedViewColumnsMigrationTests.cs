using System.Text.Json.Nodes;
using Exceptionless.Core.Migrations;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class MigrateSavedViewColumnsMigrationTests
{
    [Fact]
    public void TryMigrate_LegacyColumnsAndOrder_ConvertsToStructuredColumns()
    {
        // Arrange
        var source = JsonNode.Parse(
            """
            {
              "columns": {
                "project": true,
                "summary": false
              },
              "column_order": [
                "summary",
                "project",
                "date"
              ]
            }
            """
        )!.AsObject();

        // Act
        bool migrated = MigrateSavedViewColumns.TryMigrate(source);

        // Assert
        Assert.True(migrated);
        Assert.False(source.ContainsKey("column_order"));
        var columns = Assert.IsType<JsonObject>(source["columns"]);
        Assert.False(columns["summary"]!["visible"]!.GetValue<bool>());
        Assert.Equal(0, columns["summary"]!["position"]!.GetValue<int>());
        Assert.True(columns["project"]!["visible"]!.GetValue<bool>());
        Assert.Equal(1, columns["project"]!["position"]!.GetValue<int>());
        Assert.Equal(2, columns["date"]!["position"]!.GetValue<int>());
        Assert.Null(columns["date"]!["visible"]);
    }

    [Fact]
    public void TryMigrate_StructuredColumnsWithoutLegacyOrder_DoesNotChangeDocument()
    {
        // Arrange
        var source = JsonNode.Parse(
            """
            {
              "columns": {
                "project": {
                  "visible": true,
                  "position": 0,
                  "width": 320
                }
              }
            }
            """
        )!.AsObject();
        string originalJson = source.ToJsonString();

        // Act
        bool migrated = MigrateSavedViewColumns.TryMigrate(source);

        // Assert
        Assert.False(migrated);
        Assert.Equal(originalJson, source.ToJsonString());
    }

    [Fact]
    public void TryMigrate_PartiallyMigratedDocument_PreservesExistingWidths()
    {
        // Arrange
        var source = JsonNode.Parse(
            """
            {
              "columns": {
                "project": {
                  "visible": true,
                  "width": 360
                }
              },
              "column_order": [
                "project"
              ]
            }
            """
        )!.AsObject();

        // Act
        bool migrated = MigrateSavedViewColumns.TryMigrate(source);

        // Assert
        Assert.True(migrated);
        var project = source["columns"]!["project"]!;
        Assert.True(project["visible"]!.GetValue<bool>());
        Assert.Equal(0, project["position"]!.GetValue<int>());
        Assert.Equal(360, project["width"]!.GetValue<int>());
    }
}
