using System.Globalization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Lock;
using Foundatio.Repositories.Elasticsearch.CustomFields;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

public class EventCustomFieldService : IStartupAction
{
    private readonly IEventRepository _eventRepository;
    private readonly ICustomFieldDefinitionRepository _customFieldDefinitionRepository;
    private readonly ILockProvider _lockProvider;
    private readonly ILogger<EventCustomFieldService> _logger;

    private const int MaxKeywordLength = 256;

    /// <summary>
    /// System fields that are auto-provisioned per organization and cannot be deleted.
    /// These support the session system and core event metadata.
    /// Because they are always provisioned first in their respective types, their slot numbers are deterministic.
    /// </summary>
    public static readonly IReadOnlyList<(string Name, string IndexType)> SystemFields =
    [
        ("@ref:session", "keyword"),
        (Event.KnownDataKeys.SessionEnd, "date"),
        (Event.KnownDataKeys.SessionHasError, "bool")
    ];

    /// <summary>
    /// Well-known idx field names for system fields. These are deterministic because system fields
    /// are always provisioned first via EnsureSystemFieldsAsync (slot 1 for each type).
    /// </summary>
    public const string SessionReferenceIdxField = "keyword-1";
    public const string SessionEndIdxField = "date-1";
    public const string SessionHasErrorIdxField = "bool-1";

    /// <summary>
    /// The set of index types registered by <c>AddStandardCustomFieldTypes()</c> in <c>EventIndex</c>.
    /// Only these types are supported for custom field definitions; any other type string would result
    /// in an un-indexed, unqueryable field.
    /// </summary>
    public static readonly IReadOnlySet<string> SupportedIndexTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "bool", "date", "double", "float", "int", "keyword", "long", "string"
    };

    public EventCustomFieldService(
        IEventRepository eventRepository,
        ICustomFieldDefinitionRepository customFieldDefinitionRepository,
        ILockProvider lockProvider,
        ILoggerFactory loggerFactory)
    {
        _eventRepository = eventRepository;
        _customFieldDefinitionRepository = customFieldDefinitionRepository;
        _lockProvider = lockProvider;
        _logger = loggerFactory.CreateLogger<EventCustomFieldService>();
    }

    public Task RunAsync(CancellationToken shutdownToken = default)
    {
        _eventRepository.DocumentsChanging.AddHandler(OnDocumentsChangingAsync);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ensures system fields exist for the given organization. Returns true if any were created.
    /// </summary>
    public async Task EnsureSystemFieldsAsync(string organizationId)
    {
        var existing = await _customFieldDefinitionRepository.GetFieldMappingAsync(nameof(PersistentEvent), organizationId);

        foreach (var (name, indexType) in SystemFields)
        {
            if (existing.ContainsKey(name))
                continue;

            await _customFieldDefinitionRepository.AddFieldAsync(
                nameof(PersistentEvent), organizationId, name, indexType,
                description: $"System field: {name}");
        }
    }

    /// <summary>
    /// Returns true if the given field name is a system/reserved field that cannot be deleted.
    /// </summary>
    public static bool IsSystemField(string fieldName)
    {
        return SystemFields.Any(f => String.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Creates a new custom field definition under a distributed lock so concurrent requests
    /// from the same organization cannot race past the quota check.
    /// Returns null when the field cannot be created (quota exceeded or duplicate name).
    /// </summary>
    public async Task<CustomFieldDefinition?> CreateFieldAsync(
        string organizationId,
        string name,
        string indexType,
        int maxFieldsPerOrganization,
        string? description = null,
        int? displayOrder = null,
        CancellationToken cancellationToken = default)
    {
        // Ensure system fields are provisioned under the lock so they always occupy slot 1 of their type.
        await EnsureSystemFieldsAsync(organizationId);

        await using var fieldLock = await _lockProvider.AcquireAsync($"custom-field-create:{organizationId}", TimeSpan.FromSeconds(30), cancellationToken: cancellationToken);
        if (fieldLock is null)
        {
            _logger.LogWarning("Could not acquire custom field creation lock for organization {OrganizationId}", organizationId);
            return null;
        }

        // Re-read the field mapping inside the lock for an accurate count.
        var existingPage = await _customFieldDefinitionRepository.FindByTenantAsync(nameof(PersistentEvent), organizationId);
        var allActive = new List<CustomFieldDefinition>(existingPage.Documents);
        while (await existingPage.NextPageAsync())
            allActive.AddRange(existingPage.Documents);

        // System fields are not counted against the user quota.
        var userDefinedActiveCount = allActive.Count(f => !IsSystemField(f.Name));
        if (userDefinedActiveCount >= maxFieldsPerOrganization)
            return null;

        // Case-insensitive duplicate check inside the lock.
        if (allActive.Any(f => String.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
            return null;

        return await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), organizationId, name, indexType, description, displayOrder ?? 0);
    }

    private async Task OnDocumentsChangingAsync(object sender, DocumentsChangeEventArgs<PersistentEvent> args)
    {
        if (args.ChangeType == ChangeType.Removed)
            return;

        if (args.Documents is null || args.Documents.Count == 0)
            return;

        var documentsByOrganization = args.Documents
            .Select(d => d.Value)
            .OfType<PersistentEvent>()
            .GroupBy(d => d.OrganizationId)
            .Where(g => !String.IsNullOrEmpty(g.Key));

        foreach (var organizationGroup in documentsByOrganization)
        {
            IDictionary<string, CustomFieldDefinition>? fieldMapping = null;
            try
            {
                fieldMapping = await _customFieldDefinitionRepository.GetFieldMappingAsync(nameof(PersistentEvent), organizationGroup.Key);

                // Lazily ensure ALL system fields are provisioned for this org.
                // Check each system field individually to handle partial-provisioning failures.
                if (SystemFields.Any(f => !fieldMapping.ContainsKey(f.Name)))
                {
                    await EnsureSystemFieldsAsync(organizationGroup.Key);
                    fieldMapping = await _customFieldDefinitionRepository.GetFieldMappingAsync(nameof(PersistentEvent), organizationGroup.Key);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error loading custom field definitions for organization {OrganizationId}", organizationGroup.Key);
                continue;
            }

            foreach (var document in organizationGroup)
            {
                try
                {
                    ProcessEventCustomFields(document, fieldMapping);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error processing custom fields for event {EventId}", document.Id);
                }
            }
        }
    }

    private void ProcessEventCustomFields(PersistentEvent ev, IDictionary<string, CustomFieldDefinition> fieldMapping)
    {
        ClearCustomFieldSlots(ev);

        if (fieldMapping.Count == 0 || ev.Data is null || ev.Data.Count == 0)
            return;

        var idx = ((IHaveVirtualCustomFields)ev).Idx;

        // Iterate the field mapping (max ~20 entries) rather than all of ev.Data
        // to avoid allocating an intermediate dictionary for events with large payloads.
        // DataDictionary uses OrdinalIgnoreCase so the lookup is case-insensitive.
        foreach (var (fieldName, definition) in fieldMapping)
        {
            if (definition.IsDeleted)
                continue;

            if (!ev.Data.TryGetValue(fieldName, out var rawValue) || rawValue is null)
                continue;

            // Only primitive types are indexable (mirrors GetCustomFields filtering).
            if (rawValue is not (string or bool or int or long or float or double or decimal or DateTime or DateTimeOffset))
                continue;

            try
            {
                var value = ConvertValue(rawValue, definition.IndexType);
                if (value is not null)
                    idx[definition.GetIdxName()] = value;
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                _logger.LogDebug(ex, "Skipping custom field {FieldName}: type mismatch for index type {IndexType}", fieldName, definition.IndexType);
            }
        }

        if (ev.Idx?.Count == 0)
            ev.Idx = null;
    }

    private static void ClearCustomFieldSlots(PersistentEvent ev)
    {
        if (ev.Idx is null || ev.Idx.Count == 0)
            return;

        // Only clear new-format managed slot keys (type-N, e.g. keyword-1, date-2).
        // Legacy keys (e.g. sessionend-d from pre-PR data) are preserved so that ES queries
        // that check both formats (GetOpenSessionsAsync) remain backward-compatible.
        // Client-injected new-format slots are therefore stripped before server re-population.
        foreach (var idxKey in ev.Idx.Keys.Where(IsManagedCustomFieldSlotKey).ToArray())
            ev.Idx.Remove(idxKey);

        if (ev.Idx.Count == 0)
            ev.Idx = null;
    }

    private static bool IsManagedCustomFieldSlotKey(string idxKey)
    {
        if (String.IsNullOrWhiteSpace(idxKey))
            return false;

        int separatorIndex = idxKey.LastIndexOf('-');
        if (separatorIndex <= 0 || separatorIndex == idxKey.Length - 1)
            return false;

        return SupportedIndexTypes.Contains(idxKey[..separatorIndex])
            && Int32.TryParse(idxKey.AsSpan(separatorIndex + 1), out _);
    }

    /// <summary>
    /// Strictly converts a value to the target index type. Returns null if conversion
    /// is not possible (value is skipped rather than failing event ingestion).
    /// </summary>
    public static object? ConvertValue(object? value, string indexType)
    {
        if (value is null)
            return null;

        return indexType switch
        {
            "keyword" => ConvertToKeyword(value),
            "string" => ConvertToString(value),
            "bool" => ConvertToBool(value),
            "int" => ConvertToInt(value),
            "long" => ConvertToLong(value),
            "float" => ConvertToFloat(value),
            "double" => ConvertToDouble(value),
            "date" => ConvertToDate(value),
            _ => null
        };
    }

    private static object? ConvertToKeyword(object value)
    {
        string? str = FormatInvariant(value);
        if (str is null || str.Length > MaxKeywordLength)
            return null;
        return str;
    }

    private static object? ConvertToString(object value)
    {
        string? str = FormatInvariant(value);
        if (str is null || str.Length > 8192)
            return null;
        return str;
    }

    /// <summary>
    /// Formats a primitive value to a culture-invariant string suitable for keyword/string ES fields.
    /// Using <see cref="object.ToString()"/> without a format provider would produce locale-dependent
    /// output for float/double/decimal (e.g., "1,5" on German servers) and non-ISO DateTime strings.
    /// </summary>
    private static string? FormatInvariant(object value)
    {
        return value switch
        {
            string s => s,
            bool b => b.ToString(),                                                                 // "True"/"False"
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            DateTime dt when dt.Kind != DateTimeKind.Unspecified => dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            DateTime dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            _ => null
        };
    }

    private static object? ConvertToBool(object value)
    {
        return value switch
        {
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            string s when s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1" => (object)true,
            string s when s.Equals("false", StringComparison.OrdinalIgnoreCase) || s == "0" => (object)false,
            _ => null
        };
    }

    private static object? ConvertToInt(object value)
    {
        return value switch
        {
            int i => i,
            short s => (int)s,
            byte b => (int)b,
            sbyte sb => (int)sb,
            long l when l is >= Int32.MinValue and <= Int32.MaxValue => (int)l,
            double d when Double.IsFinite(d) && d is >= Int32.MinValue and <= Int32.MaxValue => (int)d,
            float f when Single.IsFinite(f) && f is >= Int32.MinValue and <= Int32.MaxValue => (int)f,
            decimal m when m is >= Int32.MinValue and <= Int32.MaxValue => (int)m,
            string s when Int32.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    private static object? ConvertToLong(object value)
    {
        return value switch
        {
            long l => l,
            int i => (long)i,
            short s => (long)s,
            byte b => (long)b,
            sbyte sb => (long)sb,
            double d when Double.IsFinite(d) && d >= (double)Int64.MinValue && d < (double)Int64.MaxValue => (long)d,
            float f when Single.IsFinite(f) && f >= (float)Int64.MinValue && f < (float)Int64.MaxValue => (long)f,
            decimal m when m is >= Int64.MinValue and <= Int64.MaxValue => (long)m,
            string s when Int64.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    private static object? ConvertToFloat(object value)
    {
        return value switch
        {
            float f when Single.IsFinite(f) => f,
            int i => (float)i,
            // long range must be checked at runtime; Single.MinValue/MaxValue don't fit in long constants
            long l => l >= -16777216L && l <= 16777216L ? (float)l : (object?)null,
            double d when Double.IsFinite(d) && d is >= Single.MinValue and <= Single.MaxValue => (float)d,
            decimal m => (float)m,
            string s when Single.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && Single.IsFinite(parsed) => parsed,
            _ => null
        };
    }

    private static object? ConvertToDouble(object value)
    {
        return value switch
        {
            double d when Double.IsFinite(d) => d,
            float f when Single.IsFinite(f) => (double)f,
            int i => (double)i,
            long l => (double)l,
            decimal m => (double)m,
            string s when Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && Double.IsFinite(parsed) => parsed,
            _ => null
        };
    }

    private static object? ConvertToDate(object value)
    {
        return value switch
        {
            DateTime dt when dt.Kind != DateTimeKind.Unspecified => dt.ToUniversalTime(),
            DateTime dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
            DateTimeOffset dto => dto.UtcDateTime,
            // AssumeUniversal treats strings without explicit timezone info as UTC, avoiding
            // silent server-local-time interpretation. Strings with explicit offsets use those offsets.
            string s when DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) => parsed.UtcDateTime,
            _ => null
        };
    }

    /// <summary>
    /// Validates that a custom field name meets requirements:
    /// - Not empty, max 100 chars
    /// - Any name starting with '@' is reserved
    /// - Only ASCII letters, digits, underscore, dot, dash allowed (no Unicode)
    /// </summary>
    public static bool IsValidFieldName(string name)
    {
        if (String.IsNullOrWhiteSpace(name))
            return false;

        if (name.Length > 100)
            return false;

        // Any @-prefixed name is reserved
        if (name.StartsWith('@'))
            return false;

        // Only ASCII alphanumeric, underscore, dot, and dash — no Unicode identifiers
        return name.All(c => Char.IsAsciiLetterOrDigit(c) || c == '_' || c == '.' || c == '-');
    }

}
