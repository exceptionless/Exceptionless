using Exceptionless.Core.Models;
using Foundatio.Repositories.Elasticsearch.CustomFields;
using Xunit;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;

namespace Exceptionless.Tests.CustomFields;

public class PersistentEventCustomFieldsTests
{
    [Fact]
    public void GetTenantKey_ReturnsOrganizationId()
    {
        var ev = new PersistentEvent { OrganizationId = "org123" };
        Assert.Equal("org123", ev.GetTenantKey());
    }

    [Fact]
    public void GetCustomFields_ReturnsPrimitiveValuesFromData()
    {
        var ev = new PersistentEvent
        {
            OrganizationId = "org1",
            Data = new DataDictionary
            {
                { "duration", 42 },
                { "is_active", true },
                { "label", "test" },
                { "amount", 3.14 }
            }
        };

        var fields = ev.GetCustomFields();

        Assert.Equal(4, fields.Count);
        Assert.Equal(42, fields["duration"]);
        Assert.Equal(true, fields["is_active"]);
        Assert.Equal("test", fields["label"]);
        Assert.Equal(3.14, fields["amount"]);
    }

    [Fact]
    public void GetCustomFields_SkipsKeysStartingWithAt()
    {
        var ev = new PersistentEvent
        {
            OrganizationId = "org1",
            Data = new DataDictionary
            {
                { "@error", "some error object" },
                { "@request", "request data" },
                { "custom_field", "value" }
            }
        };

        var fields = ev.GetCustomFields();

        Assert.Single(fields);
        Assert.True(fields.ContainsKey("custom_field"));
        Assert.False(fields.ContainsKey("@error"));
        Assert.False(fields.ContainsKey("@request"));
    }

    [Fact]
    public void GetCustomFields_SkipsNonPrimitiveValues()
    {
        var ev = new PersistentEvent
        {
            OrganizationId = "org1",
            Data = new DataDictionary
            {
                { "simple", "hello" },
                { "complex", new { Nested = true } },
                { "list", new List<int> { 1, 2, 3 } }
            }
        };

        var fields = ev.GetCustomFields();

        Assert.Single(fields);
        Assert.Equal("hello", fields["simple"]);
    }

    [Fact]
    public void GetCustomFields_ReturnsEmpty_WhenDataIsNull()
    {
        var ev = new PersistentEvent { OrganizationId = "org1", Data = null };

        var fields = ev.GetCustomFields();

        Assert.Empty(fields);
    }

    [Fact]
    public void SetCustomField_CreatesDataIfNull_AndSetsValue()
    {
        var ev = new PersistentEvent { OrganizationId = "org1", Data = null };

        ev.SetCustomField("my_field", 42);

        Assert.NotNull(ev.Data);
        Assert.Equal(42, ev.Data["my_field"]);
    }

    [Fact]
    public void SetCustomField_OverwritesExistingValue()
    {
        var ev = new PersistentEvent
        {
            OrganizationId = "org1",
            Data = new DataDictionary { { "field", "old" } }
        };

        ev.SetCustomField("field", "new");

        Assert.Equal("new", ev.Data!["field"]);
    }

    [Fact]
    public void RemoveCustomField_RemovesFromData()
    {
        var ev = new PersistentEvent
        {
            OrganizationId = "org1",
            Data = new DataDictionary { { "field", "value" } }
        };

        ev.RemoveCustomField("field");

        Assert.False(ev.Data!.ContainsKey("field"));
    }

    [Fact]
    public void RemoveCustomField_DoesNotThrow_WhenDataIsNull()
    {
        var ev = new PersistentEvent { OrganizationId = "org1", Data = null };

        // Should not throw
        ev.RemoveCustomField("nonexistent");
    }

    [Fact]
    public void Idx_ExplicitInterface_InitializesDataDictionary_WhenNull()
    {
        var ev = new PersistentEvent { OrganizationId = "org1", Idx = null };
        var virtualFields = (IHaveVirtualCustomFields)ev;

        var idx = virtualFields.Idx;

        Assert.NotNull(idx);
        Assert.NotNull(ev.Idx);
    }

    [Fact]
    public void GetCustomField_ReturnsValueFromData()
    {
        var ev = new PersistentEvent
        {
            OrganizationId = "org1",
            Data = new DataDictionary { { "my_field", 99 } }
        };

        var value = ev.GetCustomField("my_field");

        Assert.Equal(99, value);
    }

    [Fact]
    public void GetCustomField_ReturnsNull_WhenKeyNotFound()
    {
        var ev = new PersistentEvent
        {
            OrganizationId = "org1",
            Data = new DataDictionary()
        };

        var value = ev.GetCustomField("nonexistent");

        Assert.Null(value);
    }

    [Fact]
    public void GetCustomFields_IncludesDateTimeValues()
    {
        var now = DateTime.UtcNow;
        var ev = new PersistentEvent
        {
            OrganizationId = "org1",
            Data = new DataDictionary { { "timestamp", now } }
        };

        var fields = ev.GetCustomFields();

        Assert.Single(fields);
        Assert.Equal(now, fields["timestamp"]);
    }

    [Fact]
    public void GetCustomFields_IncludesDecimalValues()
    {
        var ev = new PersistentEvent
        {
            OrganizationId = "org1",
            Data = new DataDictionary { { "price", 19.99m } }
        };

        var fields = ev.GetCustomFields();

        Assert.Single(fields);
        Assert.Equal(19.99m, fields["price"]);
    }
}
