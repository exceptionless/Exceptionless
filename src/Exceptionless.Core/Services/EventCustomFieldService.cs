using System.Globalization;
using Exceptionless;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.CustomFields;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

public class EventCustomFieldService : IStartupAction
{
    private readonly IEventRepository _eventRepository;
    private readonly ICustomFieldDefinitionRepository _customFieldDefinitionRepository;
    private readonly ILockProvider _lockProvider;
    private readonly ILogger<EventCustomFieldService> _logger;

    private const int MaxKeywordLength = 256;

    public const string SessionReferenceIdxField = "keyword-1";
    public const string SessionEndIdxField = "date-1";
    public const string SessionHasErrorIdxField = "bool-1";

    /// <summary>
    /// Canonical session field definitions shared by provisioning, indexing, and query compatibility.
    /// </summary>
    public static readonly SystemFieldDescriptor SessionReferenceField =
        new("@ref:session", "keyword", SessionReferenceIdxField, "session-r");
    public static readonly SystemFieldDescriptor SessionEndField =
        new(Event.KnownDataKeys.SessionEnd, "date", SessionEndIdxField, "sessionend-d");
    public static readonly SystemFieldDescriptor SessionHasErrorField =
        new(Event.KnownDataKeys.SessionHasError, "bool", SessionHasErrorIdxField, "haserror-b");

    public static readonly IReadOnlyList<SystemFieldDescriptor> SystemFields =
    [
        SessionReferenceField,
        SessionEndField,
        SessionHasErrorField
    ];

    public static string GetSavedViewConsistencyLockName(string organizationId)
        => $"custom-field-saved-views:{organizationId}";

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
    /// Ensures system fields exist for the given organization and occupy their reserved slots.
    /// Invalid persisted state is rejected rather than silently creating definitions that queries cannot read.
    /// </summary>
    public Task EnsureSystemFieldsAsync(string organizationId)
        => EnsureSystemFieldsAsync(organizationId, CancellationToken.None);

    private async Task EnsureSystemFieldsAsync(string organizationId, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureSystemFieldsCoreAsync(organizationId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppDiagnostics.CustomFieldProvisioningFailures.Add(1);
            throw;
        }
    }

    private async Task EnsureSystemFieldsCoreAsync(string organizationId, CancellationToken cancellationToken)
    {
        await using var provisioningLock = await _lockProvider.TryAcquireAsync(
            $"custom-field-system:{organizationId}",
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(5));
        if (provisioningLock is null)
            throw new TimeoutException("System custom field provisioning is already in progress for this organization. Please try again.");

        var results = await _customFieldDefinitionRepository.FindAsync(
            q => q
                .FieldEquals(field => field.EntityType, nameof(PersistentEvent))
                .FieldEquals(field => field.TenantKey, organizationId),
            o => o.IncludeSoftDeletes().SearchAfterPaging().PageLimit(1000));

        var definitions = new List<CustomFieldDefinition>();
        do
        {
            definitions.AddRange(results.Documents);
        } while (await results.NextPageAsync());

        foreach (var systemField in SystemFields)
        {
            var namedDefinitions = definitions
                .Where(definition => String.Equals(definition.Name, systemField.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (namedDefinitions.Count > 1)
                throw CreateSystemFieldConflict(systemField, $"found {namedDefinitions.Count} definitions with the reserved name");

            if (namedDefinitions.Count == 1)
            {
                ValidateSystemFieldDefinition(systemField, namedDefinitions[0], organizationId);
                continue;
            }

            var slotOccupant = definitions.FirstOrDefault(definition =>
                String.Equals(definition.IndexType, systemField.IndexType, StringComparison.Ordinal)
                && String.Equals(definition.GetIdxName(), systemField.IdxField, StringComparison.Ordinal));
            if (slotOccupant is not null)
                throw CreateSystemFieldConflict(systemField, $"reserved slot is occupied by '{slotOccupant.Name}'");

            var definition = await _customFieldDefinitionRepository.AddFieldAsync(
                nameof(PersistentEvent), organizationId, systemField.Name, systemField.IndexType,
                description: $"System field: {systemField.Name}");
            ValidateSystemFieldDefinition(systemField, definition, organizationId);
            definitions.Add(definition);
        }
    }

    /// <summary>
    /// Returns true if the given field name is a system/reserved field that cannot be deleted.
    /// </summary>
    public static bool IsSystemField(string fieldName)
    {
        return TryGetSystemField(fieldName, out _);
    }

    public static bool TryGetSystemField(string fieldName, out SystemFieldDescriptor descriptor)
    {
        descriptor = SystemFields.FirstOrDefault(field => String.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase))!;
        return descriptor is not null;
    }

    /// <summary>
    /// Creates a new custom field definition under a distributed lock so concurrent requests
    /// from the same organization cannot race past the quota check.
    /// Returns a typed outcome so callers can distinguish duplicate names from capacity limits.
    /// </summary>
    public async Task<CreateFieldResult> CreateFieldAsync(
        string organizationId,
        string name,
        string indexType,
        int maxFieldsPerOrganization,
        int maxLifetimeFieldsPerOrganization,
        string? description = null,
        int? displayOrder = null,
        CancellationToken cancellationToken = default)
    {
        // Ensure system fields are provisioned before user-defined fields so they occupy slot 1 of their type.
        await EnsureSystemFieldsAsync(organizationId, cancellationToken);

        await using var fieldLock = await _lockProvider.TryAcquireAsync(
            $"custom-field-create:{organizationId}", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5));
        if (fieldLock is null)
        {
            _logger.LogWarning("Could not acquire custom field creation lock for organization {OrganizationId}", organizationId);
            throw new TimeoutException("Custom field creation is already in progress for this organization. Please try again.");
        }

        // Re-read active and soft-deleted definitions inside the lock. Soft-deleted definitions
        // still own their physical slot and therefore count against the lifetime mapping budget.
        var existingPage = await _customFieldDefinitionRepository.FindAsync(
            q => q
                .FieldEquals(field => field.EntityType, nameof(PersistentEvent))
                .FieldEquals(field => field.TenantKey, organizationId),
            o => o.IncludeSoftDeletes().SearchAfterPaging().PageLimit(1000));
        var allDefinitions = new List<CustomFieldDefinition>();
        do
        {
            allDefinitions.AddRange(existingPage.Documents);
        } while (await existingPage.NextPageAsync());

        var activeDefinitions = allDefinitions.Where(field => !field.IsDeleted).ToList();

        if (activeDefinitions.Any(field => String.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)))
            return new CreateFieldResult(CreateFieldStatus.Duplicate);

        // System fields are not counted against the user quota.
        var userDefinedActiveCount = activeDefinitions.Count(field => !IsSystemField(field.Name));
        if (userDefinedActiveCount >= maxFieldsPerOrganization)
            return new CreateFieldResult(CreateFieldStatus.ActiveLimitReached);

        var userDefinedLifetimeCount = allDefinitions.Count(field => !IsSystemField(field.Name));
        if (userDefinedLifetimeCount >= maxLifetimeFieldsPerOrganization)
        {
            AppDiagnostics.CustomFieldLifetimeLimitReached.Add(1);
            return new CreateFieldResult(CreateFieldStatus.LifetimeLimitReached);
        }

        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), organizationId, name, indexType, description, displayOrder ?? 0);
        return new CreateFieldResult(CreateFieldStatus.Created, definition);
    }

    private async Task OnDocumentsChangingAsync(object sender, DocumentsChangeEventArgs<PersistentEvent> args)
    {
        if (args.ChangeType == ChangeType.Removed)
            return;

        if (args.Documents is null || args.Documents.Count == 0)
            return;

        var documentsByOrganization = args.Documents
            .Where(document => document.Value is not null)
            .GroupBy(document => document.Value.OrganizationId)
            .Where(g => !String.IsNullOrEmpty(g.Key));

        foreach (var organizationGroup in documentsByOrganization)
        {
            IDictionary<string, CustomFieldDefinition>? fieldMapping = null;
            try
            {
                fieldMapping = await _customFieldDefinitionRepository.GetFieldMappingAsync(nameof(PersistentEvent), organizationGroup.Key);

                // Lazily ensure all system fields are provisioned for this organization.
                // Check each system field individually to handle partial-provisioning failures.
                if (SystemFields.Any(field => !fieldMapping.ContainsKey(field.Name)))
                {
                    await EnsureSystemFieldsAsync(organizationGroup.Key);
                    fieldMapping = await _customFieldDefinitionRepository.GetFieldMappingAsync(nameof(PersistentEvent), organizationGroup.Key);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AppDiagnostics.CustomFieldMappingFailures.Add(1);
                _logger.LogError(ex, "Error loading custom field definitions for organization {OrganizationId}", organizationGroup.Key);

                if (args.ChangeType == ChangeType.Added)
                {
                    foreach (var document in organizationGroup)
                        ClearCustomFieldSlots(document.Value);

                    continue;
                }

                // Never persist a saved event after stripping or partially rebuilding its slots.
                // The caller can retry once the definition repository is available again.
                throw;
            }

            foreach (var document in organizationGroup)
            {
                try
                {
                    document.Value.Idx = BuildCustomFieldSlots(
                        document.Value,
                        fieldMapping,
                        preserveUnmanagedSlots: args.ChangeType != ChangeType.Added);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    AppDiagnostics.CustomFieldProcessingFailures.Add(1);
                    _logger.LogError(ex, "Error processing custom fields for event {EventId}", document.Value.Id);

                    if (args.ChangeType == ChangeType.Added)
                    {
                        ClearCustomFieldSlots(document.Value);
                        continue;
                    }

                    throw;
                }
            }
        }
    }

    private DataDictionary? BuildCustomFieldSlots(
        PersistentEvent ev,
        IDictionary<string, CustomFieldDefinition> fieldMapping,
        bool preserveUnmanagedSlots)
    {
        var idx = !preserveUnmanagedSlots || ev.Idx is null
            ? new DataDictionary()
            : new DataDictionary(ev.Idx.Where(field => !IsManagedCustomFieldSlotKey(field.Key)));

        if (fieldMapping.Count == 0 || ev.Data is null || ev.Data.Count == 0)
            return idx.Count == 0 ? null : idx;

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
                else
                    AppDiagnostics.CustomFieldConversionSkips.Add(1, new KeyValuePair<string, object?>("index_type", definition.IndexType));
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                AppDiagnostics.CustomFieldConversionSkips.Add(1, new KeyValuePair<string, object?>("index_type", definition.IndexType));
                _logger.LogDebug(ex, "Skipping custom field {FieldName}: type mismatch for index type {IndexType}", fieldName, definition.IndexType);
            }
        }

        return idx.Count == 0 ? null : idx;
    }

    private static void ClearCustomFieldSlots(PersistentEvent ev)
    {
        // Idx is server-managed. New events must never persist client-supplied pooled or
        // legacy compatibility slots; saved legacy events preserve unmanaged slots above.
        ev.Idx = null;
    }

    public static bool IsManagedCustomFieldSlotKey(string idxKey)
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
            double d when Double.IsFinite(d) && d is >= Int32.MinValue and <= Int32.MaxValue && Math.Truncate(d) == d => (int)d,
            float f when Single.IsFinite(f) && f is >= Int32.MinValue and <= Int32.MaxValue && MathF.Truncate(f) == f => (int)f,
            decimal m when m is >= Int32.MinValue and <= Int32.MaxValue && Decimal.Truncate(m) == m => (int)m,
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
            double d when Double.IsFinite(d) && d >= Int64.MinValue && d < Int64.MaxValue && Math.Truncate(d) == d => (long)d,
            float f when Single.IsFinite(f) && f >= Int64.MinValue && f < Int64.MaxValue && MathF.Truncate(f) == f => (long)f,
            decimal m when m is >= Int64.MinValue and <= Int64.MaxValue && Decimal.Truncate(m) == m => (long)m,
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
            long l => (float)l,
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

        // Physical pooled slots and legacy compatibility slots are implementation details.
        // Allowing a logical definition with one of these names would make idx.<name>
        // ambiguous or unqueryable.
        if (IsManagedCustomFieldSlotKey(name)
            || SystemFields.Any(field => String.Equals(field.LegacyIdxField, name, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Only ASCII alphanumeric, underscore, dot, and dash — no Unicode identifiers
        return name.All(c => Char.IsAsciiLetterOrDigit(c) || c == '_' || c == '.' || c == '-');
    }

    private static void ValidateSystemFieldDefinition(SystemFieldDescriptor systemField, CustomFieldDefinition definition, string organizationId)
    {
        if (!String.Equals(definition.Name, systemField.Name, StringComparison.Ordinal))
            throw CreateSystemFieldConflict(systemField, $"name is '{definition.Name}'");

        if (!String.Equals(definition.EntityType, nameof(PersistentEvent), StringComparison.Ordinal))
            throw CreateSystemFieldConflict(systemField, $"entity type is '{definition.EntityType}'");

        if (!String.Equals(definition.TenantKey, organizationId, StringComparison.Ordinal))
            throw CreateSystemFieldConflict(systemField, $"tenant is '{definition.TenantKey}'");

        if (!String.Equals(definition.IndexType, systemField.IndexType, StringComparison.Ordinal))
            throw CreateSystemFieldConflict(systemField, $"index type is '{definition.IndexType}'");

        if (!String.Equals(definition.GetIdxName(), systemField.IdxField, StringComparison.Ordinal))
            throw CreateSystemFieldConflict(systemField, $"slot is '{definition.GetIdxName()}'");

        if (definition.IsDeleted)
            throw CreateSystemFieldConflict(systemField, "definition is soft-deleted");
    }

    private static InvalidOperationException CreateSystemFieldConflict(SystemFieldDescriptor systemField, string reason)
    {
        return new InvalidOperationException(
            $"System custom field '{systemField.Name}' must be an active {systemField.IndexType} field at idx.{systemField.IdxField}, but {reason}.");
    }

    public sealed record SystemFieldDescriptor(string Name, string IndexType, string IdxField, string LegacyIdxField);

    public enum CreateFieldStatus
    {
        Created,
        Duplicate,
        ActiveLimitReached,
        LifetimeLimitReached
    }

    public sealed record CreateFieldResult(CreateFieldStatus Status, CustomFieldDefinition? Definition = null);
}
