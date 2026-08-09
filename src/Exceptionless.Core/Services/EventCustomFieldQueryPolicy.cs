using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Repositories.Elasticsearch.CustomFields;

namespace Exceptionless.Core.Services;

/// <summary>
/// Validates logical event custom-field references before repository field resolution.
/// This prevents unknown fields, raw pooled slots, and ambiguous tenant scopes from
/// degrading into queries that silently return no results.
/// </summary>
public sealed class EventCustomFieldQueryPolicy(ICustomFieldDefinitionRepository customFieldDefinitionRepository)
{
    public const string UnknownFilterField = "unknown_filter_field";
    public const string CustomFieldScopeRequired = "custom_field_scope_required";

    public async Task<ValidationResult> ValidateAsync(
        IEnumerable<string> referencedFields,
        AppFilter? appFilter,
        CancellationToken cancellationToken = default)
    {
        var customFields = referencedFields
            .Select(TryGetLogicalCustomField)
            .Where(field => field is not null)
            .Select(field => field!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (customFields.Length == 0)
            return ValidationResult.Valid;

        if (appFilter?.Organizations.Count != 1)
        {
            return ValidationResult.Invalid(
                CustomFieldScopeRequired,
                "Custom-field searches must be scoped to exactly one organization.");
        }

        string organizationId = appFilter.Organizations.Single().Id;
        var mapping = await customFieldDefinitionRepository.GetFieldMappingAsync(nameof(PersistentEvent), organizationId);
        var activeFieldNames = mapping.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string field in customFields)
        {
            string logicalName = field[(field.IndexOf('.') + 1)..];
            // Definitions created before storage-slot names became reserved remain usable.
            // Unmatched physical slots are still rejected below.
            if (activeFieldNames.Contains(logicalName))
                continue;

            if (field.StartsWith("idx.", StringComparison.OrdinalIgnoreCase)
                && EventCustomFieldService.IsManagedCustomFieldSlotKey(logicalName))
            {
                return ValidationResult.Invalid(
                    UnknownFilterField,
                    $"Raw custom-field slot '{field}' cannot be queried. Use the configured logical field name instead.",
                    field);
            }

            if (!EventCustomFieldService.IsValidFieldName(logicalName) || !activeFieldNames.Contains(logicalName))
            {
                return ValidationResult.Invalid(
                    UnknownFilterField,
                    $"Filter field '{field}' is not an active configured custom field for this organization.",
                    field);
            }
        }

        return ValidationResult.Valid;
    }

    private static string? TryGetLogicalCustomField(string field)
    {
        if (String.IsNullOrWhiteSpace(field))
            return null;

        if (field.StartsWith("data.", StringComparison.OrdinalIgnoreCase))
        {
            string logicalName = field["data.".Length..];
            if (logicalName.StartsWith('@') || EventCustomFieldService.IsSystemField(logicalName))
                return null;

            return field;
        }

        if (field.StartsWith("idx.", StringComparison.OrdinalIgnoreCase))
        {
            string logicalName = field["idx.".Length..];
            if (EventCustomFieldService.IsSystemField(logicalName)
                || EventCustomFieldService.SystemFields.Any(systemField =>
                    String.Equals(systemField.LegacyIdxField, logicalName, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            return field;
        }

        return null;
    }

    public sealed record ValidationResult(bool IsValid, string? ErrorCode = null, string? Message = null, string? Field = null)
    {
        public static ValidationResult Valid { get; } = new(true);

        public static ValidationResult Invalid(string errorCode, string message, string? field = null)
            => new(false, errorCode, message, field);
    }
}
