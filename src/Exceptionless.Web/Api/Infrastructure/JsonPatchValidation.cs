using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Foundatio.Mediator;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson.Operations;

namespace Exceptionless.Web.Api.Infrastructure;

/// <summary>
/// Validates and applies RFC 6902 JSON Patch documents with immutable path protection and operation whitelisting.
/// </summary>
public static class JsonPatchValidation
{
    private const int MaxOperationsCount = 50;

    /// <summary>
    /// Validates that no operation targets a disallowed (immutable) path, and restricts operations
    /// to replace and test only (matching the original Delta semantics of top-level property replacement).
    /// </summary>
    public static Result ValidateOperations<T>(JsonPatchDocument<T> patch, params string[] immutablePaths) where T : class
    {
        if (patch.Operations.Count == 0)
            return Result.Success();

        if (patch.Operations.Count > MaxOperationsCount)
            return Result.Invalid(ValidationError.Create("patch", $"Patch document exceeds maximum of {MaxOperationsCount} operations."));

        foreach (var operation in patch.Operations)
        {
            // Only allow replace and test operations (matching original Delta behavior)
            if (operation.OperationType != OperationType.Replace && operation.OperationType != OperationType.Test)
                return Result.Invalid(ValidationError.Create("patch", $"Operation '{operation.op}' is not supported. Only 'replace' and 'test' operations are allowed."));

            // Check immutable paths
            var normalizedPath = NormalizePath(operation.path);
            if (immutablePaths.Any(p => normalizedPath.Equals(NormalizePath(p), StringComparison.OrdinalIgnoreCase)))
                return Result.Invalid(ValidationError.Create(normalizedPath.TrimStart('/'), $"The property '{normalizedPath.TrimStart('/')}' cannot be modified."));

            // Reject nested paths (only top-level properties allowed, matching Delta semantics)
            var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 1)
                return Result.Invalid(ValidationError.Create("patch", $"Nested path '{operation.path}' is not supported. Only top-level property modifications are allowed."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Applies a patch document to a target DTO, collecting any errors from the patch engine.
    /// Returns a Result with validation errors if any operation fails.
    /// </summary>
    public static Result ApplyPatch<T>(JsonPatchDocument<T> patch, T target) where T : class
    {
        if (patch.Operations.Count == 0)
            return Result.Success();

        List<string>? errors = null;

        patch.ApplyTo(target, error =>
        {
            errors ??= [];
            errors.Add(error.ErrorMessage);
        });

        if (errors is not null)
            return Result.Invalid(errors.Select(e => ValidationError.Create("patch", e)).ToArray());

        return Result.Success();
    }

    /// <summary>
    /// Checks whether any operation in the patch targets a specific property path.
    /// Path comparison uses the JSON naming convention (snake_case).
    /// </summary>
    public static bool AffectsPath<T>(this JsonPatchDocument<T> patch, string path) where T : class
    {
        var normalized = NormalizePath(path);
        return patch.Operations.Any(op =>
            NormalizePath(op.path).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks whether any operation in the patch targets the specified property.
    /// Uses the configured naming policy to derive the JSON path from the property expression.
    /// </summary>
    public static bool AffectsProperty<T>(this JsonPatchDocument<T> patch, Expression<Func<T, object?>> property) where T : class
    {
        var memberName = GetMemberName(property);
        var jsonName = patch.SerializerOptions?.PropertyNamingPolicy?.ConvertName(memberName) ?? memberName;
        return patch.AffectsPath("/" + jsonName);
    }

    /// <summary>
    /// Gets all top-level property names (in their original C# PascalCase form) affected by the patch.
    /// Uses the naming policy in reverse to map from JSON paths back to property names.
    /// </summary>
    public static IReadOnlySet<string> GetAffectedPropertyNames<T>(this JsonPatchDocument<T> patch) where T : class
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var policy = patch.SerializerOptions?.PropertyNamingPolicy;
        var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var op in patch.Operations)
        {
            var pathSegment = NormalizePath(op.path).TrimStart('/');
            foreach (var prop in properties)
            {
                var jsonName = policy?.ConvertName(prop.Name) ?? prop.Name;
                if (pathSegment.Equals(jsonName, StringComparison.OrdinalIgnoreCase)
                    || pathSegment.Equals(prop.Name, StringComparison.OrdinalIgnoreCase))
                {
                    affected.Add(prop.Name);
                    break;
                }
            }
        }

        return affected;
    }

    /// <summary>
    /// Returns true if the patch document has no operations (nothing to update).
    /// </summary>
    public static bool IsEmpty<T>(this JsonPatchDocument<T> patch) where T : class
        => patch.Operations.Count == 0;

    private static string NormalizePath(string path)
    {
        // Ensure path starts with /
        if (!path.StartsWith('/'))
            path = "/" + path;
        // Decode JSON Pointer escapes (RFC 6901)
        return path.Replace("~1", "/").Replace("~0", "~");
    }

    private static string GetMemberName<T>(Expression<Func<T, object?>> expression)
    {
        var body = expression.Body;
        if (body is UnaryExpression unary)
            body = unary.Operand;
        if (body is MemberExpression member)
            return member.Member.Name;
        throw new ArgumentException("Expression must be a member access expression.", nameof(expression));
    }

    /// <summary>
    /// Converts a partial JSON object (e.g., from legacy v1 clients) into a typed JsonPatchDocument
    /// with "replace" operations for each property in the object.
    /// </summary>
    public static JsonPatchDocument<T>? FromPartialObject<T>(JsonElement body, JsonSerializerOptions options) where T : class
    {
        if (body.ValueKind != JsonValueKind.Object)
            return null;

        var ops = new JsonArray();
        foreach (var prop in body.EnumerateObject())
        {
            var op = new JsonObject
            {
                ["op"] = "replace",
                ["path"] = $"/{prop.Name}",
                ["value"] = JsonNode.Parse(prop.Value.GetRawText())
            };
            ops.Add(op);
        }

        if (ops.Count == 0)
            return new JsonPatchDocument<T>([], options);

        return JsonSerializer.Deserialize<JsonPatchDocument<T>>(ops.ToJsonString(), options);
    }
}
