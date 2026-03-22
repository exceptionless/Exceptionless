using System.Collections;
using System.Text.Json.Serialization.Metadata;

namespace Exceptionless.Core.Serialization;

/// <summary>
/// A type info modifier that skips empty collections during serialization to match Newtonsoft's behavior.
/// </summary>
public static class EmptyCollectionModifier
{
    /// <summary>
    /// Modifies JSON type info to skip empty collections/dictionaries during serialization.
    /// </summary>
    public static void SkipEmptyCollections(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            // For properties typed as IEnumerable (but not string), check at compile time
            if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string))
            {
                property.ShouldSerialize = (obj, value) => !IsEmptyCollection(value);
            }
            // For object-typed properties, check the runtime value
            else if (property.PropertyType == typeof(object))
            {
                var originalShouldSerialize = property.ShouldSerialize;
                property.ShouldSerialize = (obj, value) =>
                {
                    // First check original condition if any
                    if (originalShouldSerialize is not null && !originalShouldSerialize(obj, value))
                        return false;

                    // Then check if runtime value is an empty collection
                    return !IsEmptyCollection(value);
                };
            }
        }
    }

    private static bool IsEmptyCollection(object? value)
    {
        // Only skip null values. Empty collections (Count: 0) are intentional
        // and should be serialized — Newtonsoft's NullValueHandling.Ignore only
        // skipped nulls, not empty collections.
        return value is null;
    }
}
