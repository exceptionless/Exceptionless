using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Exceptionless.Tests.Utility;

/// <summary>
/// Compares two JSON strings semantically, ignoring null properties, empty arrays,
/// and empty objects that differ between Newtonsoft and STJ serialization.
/// </summary>
public static class JsonAssert
{
    public static void AssertJsonEquals(string expectedJson, string actualJson)
    {
        using var expected = JsonDocument.Parse(expectedJson);
        using var actual = JsonDocument.Parse(actualJson);
        AssertJsonElementEquals(expected.RootElement, actual.RootElement, "$");
    }

    private static void AssertJsonElementEquals(JsonElement expected, JsonElement actual, string path)
    {
        Assert.True(expected.ValueKind == actual.ValueKind,
            $"{path}: expected {expected.ValueKind} but was {actual.ValueKind}.\nExpected: {expected.GetRawText()}\nActual: {actual.GetRawText()}");

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedProperties = expected.EnumerateObject().ToList();
                var actualProperties = actual.EnumerateObject().ToList();
                Assert.True(expectedProperties.Count == actualProperties.Count,
                    $"{path}: expected {expectedProperties.Count} properties but found {actualProperties.Count}.\nExpected: {expected.GetRawText()}\nActual: {actual.GetRawText()}");

                foreach (JsonProperty expectedProperty in expectedProperties)
                {
                    Assert.True(actual.TryGetProperty(expectedProperty.Name, out JsonElement actualProperty),
                        $"{path}: missing property \"{expectedProperty.Name}\".\nExpected: {expected.GetRawText()}\nActual: {actual.GetRawText()}");
                    AssertJsonElementEquals(expectedProperty.Value, actualProperty, $"{path}.{expectedProperty.Name}");
                }
                break;
            case JsonValueKind.Array:
                var expectedItems = expected.EnumerateArray().ToList();
                var actualItems = actual.EnumerateArray().ToList();
                Assert.True(expectedItems.Count == actualItems.Count,
                    $"{path}: expected {expectedItems.Count} items but found {actualItems.Count}.\nExpected: {expected.GetRawText()}\nActual: {actual.GetRawText()}");

                for (int i = 0; i < expectedItems.Count; i++)
                    AssertJsonElementEquals(expectedItems[i], actualItems[i], $"{path}[{i}]");
                break;
            case JsonValueKind.String:
                Assert.True(expected.GetString() == actual.GetString(),
                    $"{path}: expected {expected.GetRawText()} but was {actual.GetRawText()}.");
                break;
            case JsonValueKind.Number:
                Assert.True(expected.GetRawText() == actual.GetRawText(),
                    $"{path}: expected {expected.GetRawText()} but was {actual.GetRawText()}.");
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                break;
            default:
                throw new NotSupportedException($"Unsupported JSON value kind {expected.ValueKind} at {path}.");
        }
    }

    public static void AssertJsonEquivalent(string expectedJson, string actualJson)
    {
        var expected = JsonNode.Parse(expectedJson);
        var actual = JsonNode.Parse(actualJson);
        RemoveNullAndEmptyProperties(expected);
        RemoveNullAndEmptyProperties(actual);
        Assert.True(JsonNode.DeepEquals(expected, actual),
            $"Expected:\n{expected?.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}\n\nActual:\n{actual?.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}");
    }

    private static void RemoveNullAndEmptyProperties(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var keysToRemove = new List<string>();
            foreach (var prop in obj)
            {
                if (prop.Value is null)
                    keysToRemove.Add(prop.Key);
                else if (prop.Value is JsonArray arr && arr.Count == 0)
                    keysToRemove.Add(prop.Key);
                else
                {
                    RemoveNullAndEmptyProperties(prop.Value);
                    if (prop.Value is JsonObject inner && inner.Count == 0)
                        keysToRemove.Add(prop.Key);
                }
            }

            foreach (string key in keysToRemove)
                obj.Remove(key);
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
                RemoveNullAndEmptyProperties(item);
        }
    }
}
