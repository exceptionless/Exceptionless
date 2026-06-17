using System.Text.Json;
using System.Text.Json.Nodes;

namespace Exceptionless.Core.Extensions;

/// <summary>
/// Extension methods for System.Text.Json.Nodes types (JsonNode, JsonObject, JsonArray).
/// Provides helper methods for JSON manipulation during event processing and upgrades.
/// </summary>
public static class JsonNodeExtensions
{
    /// <summary>
    /// Checks if a JsonNode is null or empty (no values for objects/arrays).
    /// </summary>
    public static bool IsNullOrEmpty(this JsonNode? target)
    {
        if (target is null)
            return true;

        if (target is JsonObject obj)
            return obj.Count is 0;

        if (target is JsonArray arr)
            return arr.Count is 0;

        if (target is JsonValue val)
        {
            // Check for null value
            if (target.GetValueKind() is JsonValueKind.Null)
                return true;

            // Check for empty string
            if (target.GetValueKind() is JsonValueKind.String)
            {
                var strValue = val.GetValue<string?>();
                return string.IsNullOrEmpty(strValue);
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a property in a JsonObject is null or empty.
    /// </summary>
    public static bool IsPropertyNullOrEmpty(this JsonObject target, string name)
    {
        if (!target.TryGetPropertyValue(name, out var value))
            return true;

        return value.IsNullOrEmpty();
    }

    /// <summary>
    /// Removes a property if it is null or empty.
    /// </summary>
    /// <returns>True if the property was removed, false otherwise.</returns>
    public static bool RemoveIfNullOrEmpty(this JsonObject target, string name)
    {
        if (!target.IsPropertyNullOrEmpty(name))
            return false;

        target.Remove(name);
        return true;
    }

    /// <summary>
    /// Removes multiple properties from a JsonObject.
    /// </summary>
    public static void RemoveAll(this JsonObject target, params string[] names)
    {
        foreach (string name in names)
            target.Remove(name);
    }

    /// <summary>
    /// Removes all properties with the given names if they are null or empty, recursively.
    /// </summary>
    /// <returns>True if any properties were removed, false otherwise.</returns>
    public static bool RemoveAllIfNullOrEmpty(this JsonObject target, params string[] names)
    {
        if (target.IsNullOrEmpty())
            return false;

        var toRemove = new List<(JsonObject parent, string name)>();

        foreach (var descendant in target.DescendantsAndSelf().OfType<JsonObject>())
        {
            foreach (var name in names.Where(n => descendant.IsPropertyNullOrEmpty(n) && descendant.ContainsKey(n)))
            {
                toRemove.Add((descendant, name));
            }
        }

        foreach (var (parent, name) in toRemove)
            parent.Remove(name);

        return toRemove.Count > 0;
    }

    /// <summary>
    /// Renames a property in a JsonObject while preserving property order.
    /// If newName already exists, the renamed value takes precedence (overwrites).
    /// </summary>
    /// <returns>True if the property was renamed, false if not found.</returns>
    public static bool Rename(this JsonObject target, string currentName, string newName)
    {
        if (string.Equals(currentName, newName))
            return true;

        if (!target.TryGetPropertyValue(currentName, out var value))
            return false;

        // To preserve order, we need to rebuild the object.
        // If newName already exists as another property, the renamed value takes precedence.
        var properties = target.ToList();
        target.Clear();

        foreach (var prop in properties)
        {
            if (prop.Key == currentName)
                target.Add(newName, prop.Value);
            else if (prop.Key == newName)
                continue; // skip: the renamed value takes precedence over existing newName
            else
                target.Add(prop.Key, prop.Value);
        }

        return true;
    }

    /// <summary>
    /// Renames a property or removes it if null or empty, preserving property order.
    /// If newName already exists, the renamed value takes precedence (overwrites).
    /// </summary>
    /// <returns>True if renamed, false if removed or not found.</returns>
    public static bool RenameOrRemoveIfNullOrEmpty(this JsonObject target, string currentName, string newName)
    {
        if (!target.TryGetPropertyValue(currentName, out var value))
            return false;

        bool isNullOrEmpty = value.IsNullOrEmpty();
        if (isNullOrEmpty)
        {
            target.Remove(currentName);
            return false;
        }

        // To preserve order, we need to rebuild the object.
        // If newName already exists as another property, the renamed value takes precedence.
        var properties = target.ToList();
        target.Clear();

        foreach (var prop in properties)
        {
            if (prop.Key == currentName)
                target.Add(newName, prop.Value);
            else if (prop.Key == newName)
                continue; // skip: the renamed value takes precedence over existing newName
            else
                target.Add(prop.Key, prop.Value);
        }

        return true;
    }

    /// <summary>
    /// Moves properties from source to target, removing if null or empty.
    /// </summary>
    public static void MoveOrRemoveIfNullOrEmpty(this JsonObject target, JsonObject source, params string[] names)
    {
        foreach (string name in names.Where(source.ContainsKey))
        {
            source.TryGetPropertyValue(name, out var value);
            bool isNullOrEmpty = value.IsNullOrEmpty();
            source.Remove(name);

            if (isNullOrEmpty)
                continue;

            target.Add(name, value);
        }
    }

    /// <summary>
    /// Renames all properties with the given name recursively throughout the JSON tree.
    /// </summary>
    public static bool RenameAll(this JsonObject target, string currentName, string newName)
    {
        var objectsWithProperty = target.DescendantsAndSelf()
            .OfType<JsonObject>()
            .Where(o => o.ContainsKey(currentName))
            .ToList();

        foreach (var obj in objectsWithProperty)
        {
            obj.Rename(currentName, newName);
        }

        return objectsWithProperty.Count > 0;
    }

    /// <summary>
    /// Gets a string value from a property, or null if not found or empty.
    /// </summary>
    public static string? GetPropertyStringValue(this JsonObject target, string name)
    {
        if (target.IsPropertyNullOrEmpty(name))
            return null;

        if (!target.TryGetPropertyValue(name, out var value))
            return null;

        return value?.ToString();
    }

    /// <summary>
    /// Gets a string value from a property and removes it.
    /// </summary>
    public static string? GetPropertyStringValueAndRemove(this JsonObject target, string name)
    {
        string? value = target.GetPropertyStringValue(name);
        target.Remove(name);
        return value;
    }

    /// <summary>
    /// Enumerates all descendant nodes of a JsonNode.
    /// </summary>
    public static IEnumerable<JsonNode?> Descendants(this JsonNode? node)
    {
        if (node is null)
            yield break;

        if (node is JsonObject obj)
        {
            foreach (var prop in obj)
            {
                yield return prop.Value;
                if (prop.Value is not null)
                {
                    foreach (var desc in Descendants(prop.Value))
                        yield return desc;
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                yield return item;
                if (item is not null)
                {
                    foreach (var desc in Descendants(item))
                        yield return desc;
                }
            }
        }
    }

    /// <summary>
    /// Enumerates the node itself and all its descendants.
    /// </summary>
    public static IEnumerable<JsonNode?> DescendantsAndSelf(this JsonNode? node)
    {
        yield return node;
        foreach (var desc in Descendants(node))
            yield return desc;
    }

    /// <summary>
    /// Checks if a JsonNode has any values (for objects: has properties, for arrays: has items).
    /// </summary>
    public static bool HasValues(this JsonNode? node)
    {
        return !node.IsNullOrEmpty();
    }

    /// <summary>
    /// Converts a JsonNode to the specified type.
    /// </summary>
    public static T? ToObject<T>(this JsonNode? node, JsonSerializerOptions options)
    {
        if (node is null)
            return default;

        return node.Deserialize<T>(options);
    }

    /// <summary>
    /// Converts a JsonArray to a List of the specified type.
    /// </summary>
    public static List<T>? ToList<T>(this JsonArray? array, JsonSerializerOptions options)
    {
        if (array is null)
            return null;

        return array.Deserialize<List<T>>(options);
    }

}
