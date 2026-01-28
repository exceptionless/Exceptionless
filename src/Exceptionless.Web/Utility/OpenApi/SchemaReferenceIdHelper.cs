using System.Text.Json.Serialization.Metadata;
using Exceptionless.Web.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Primitives;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Helper for customizing OpenAPI schema reference IDs.
/// Provides consistent naming for generic types to match legacy SwashBuckle conventions.
/// </summary>
public static class SchemaReferenceIdHelper
{
    /// <summary>
    /// Creates a schema reference ID for a type, handling special cases for generic types.
    /// </summary>
    public static string? CreateSchemaReferenceId(JsonTypeInfo typeInfo)
    {
        var type = typeInfo.Type;

        // Delta<T> -> T (e.g., Delta<UpdateToken> -> UpdateToken)
        // Delta is used for PATCH operations; the schema name should match the inner type
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Delta<>))
        {
            var innerType = type.GetGenericArguments()[0];
            return innerType.Name;
        }

        // ValueFromBody<T> -> {T}ValueFromBody (e.g., ValueFromBody<string> -> StringValueFromBody)
        // This matches SwashBuckle's default naming for generic types
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueFromBody<>))
        {
            var innerType = type.GetGenericArguments()[0];
            var typeName = innerType == typeof(string) ? "String" : innerType.Name;
            return $"{typeName}ValueFromBody";
        }

        // KeyValuePair<TKey, TValue> -> {TKey}{TValue}KeyValuePair
        // (e.g., KeyValuePair<string, StringValues> -> StringStringValuesKeyValuePair)
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            var args = type.GetGenericArguments();
            var key = args[0] == typeof(string) ? "String" : args[0].Name;
            var value = args[1].Name;
            return $"{key}{value}KeyValuePair";
        }

        return OpenApiOptions.CreateDefaultSchemaReferenceId(typeInfo);
    }
}
