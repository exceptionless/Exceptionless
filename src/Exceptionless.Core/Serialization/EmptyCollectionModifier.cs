using System.Collections;
using System.Reflection;
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
                // Pre-resolve Count accessor at setup time to avoid reflection during serialization.
                // Handles types like HashSet<T> that implement ICollection<T> but not non-generic ICollection.
                var countAccessor = GetCountAccessor(property.PropertyType);
                var originalShouldSerialize = property.ShouldSerialize;

                property.ShouldSerialize = (obj, value) =>
                {
                    if (originalShouldSerialize is not null && !originalShouldSerialize(obj, value))
                        return false;

                    return !IsEmptyCollection(value, countAccessor);
                };
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
                    return !IsEmptyCollection(value, null);
                };
            }
        }
    }

    private static bool IsEmptyCollection(object? value, PropertyInfo? countAccessor)
    {
        return value switch
        {
            // Setting ShouldSerialize on a property can bypass DefaultIgnoreCondition.WhenWritingNull
            // in some .NET versions, so we must explicitly handle null here.
            null => true,
            string => false, // strings are IEnumerable but should not be treated as collections
            ICollection { Count: 0 } => true, // List<T>, Dictionary<K,V>, arrays
            _ => countAccessor is not null && (int)countAccessor.GetValue(value)! == 0
        };
    }

    /// <summary>
    /// Resolves a Count property accessor for types that implement ICollection{T} but not ICollection.
    /// Called once at type-info setup time; the PropertyInfo is reused for all serializations.
    /// </summary>
    private static PropertyInfo? GetCountAccessor(Type propertyType)
    {
        // If it already implements non-generic ICollection, the pattern match handles it fast.
        if (typeof(ICollection).IsAssignableFrom(propertyType))
            return null;

        // Check if it implements ICollection<T> (e.g., HashSet<T>)
        foreach (var iface in propertyType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(ICollection<>))
                return iface.GetProperty("Count");
        }

        // Fallback: any IEnumerable with a public Count property (custom collections)
        return propertyType.GetProperty("Count");
    }
}
