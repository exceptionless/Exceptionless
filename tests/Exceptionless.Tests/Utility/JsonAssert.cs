using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Exceptionless.Tests.Utility;

/// <summary>
/// Compares two JSON strings semantically, ignoring null properties and empty collections
/// that differ between Newtonsoft and STJ serialization.
/// </summary>
public static class JsonAssert
{
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
                    RemoveNullAndEmptyProperties(prop.Value);
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
