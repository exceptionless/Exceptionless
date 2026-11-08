using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Exceptionless.Web.Mcp;

/// <summary>
/// Builds the filter chips the app shows for a saved view, along with the filter string they produce. The app treats
/// a view as modified when the two disagree, so <see cref="ToFilter"/> must match toFilter in the app's filter models.
/// filter-definition-contract.json in the saved-views feature is tested against both.
/// </summary>
internal static partial class SavedViewFilterDefinitions
{
    /// <summary>Reads a view's chips. A view with only a filter string keeps it as a search chip.</summary>
    public static JsonArray Parse(string? filterDefinitions, string? filter)
    {
        if (!String.IsNullOrWhiteSpace(filterDefinitions))
        {
            try
            {
                if (JsonNode.Parse(filterDefinitions) is JsonArray definitions)
                    return AgreesWithFilter(definitions, filter) ? definitions : ReplaceWithSearch(definitions, filter!);
            }
            catch (JsonException)
            {
                // The app ignores definitions it cannot read, so fall back to the filter string as well.
            }
        }

        var legacyDefinitions = new JsonArray();
        if (!String.IsNullOrWhiteSpace(filter))
            Set(legacyDefinitions, "keyword", JsonValue.Create(filter));

        return legacyDefinitions;
    }

    /// <summary>
    /// True when <see cref="Parse"/> can only keep the view's filter as one search chip. Its criteria are not separate
    /// chips then, so setting a criteria would be combined with the old value instead of replacing it.
    /// </summary>
    public static bool IsRawFilter(string? filterDefinitions, string? filter)
    {
        if (String.IsNullOrWhiteSpace(filter))
            return false;

        if (String.IsNullOrWhiteSpace(filterDefinitions))
            return true;

        try
        {
            return JsonNode.Parse(filterDefinitions) is not JsonArray definitions || !AgreesWithFilter(definitions, filter);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    /// <summary>Chips are rewritten when a view changes, so a filter they do not produce would otherwise be lost.</summary>
    /// <remarks>A null filter means the chips are the view's only criteria, while an empty one means its filter was cleared.</remarks>
    private static bool AgreesWithFilter(JsonArray definitions, string? filter)
        => filter is null || String.Equals(ToFilter(definitions) ?? String.Empty, filter.Trim(), StringComparison.Ordinal);

    private static JsonArray ReplaceWithSearch(JsonArray definitions, string filter)
    {
        var replacement = new JsonArray();
        foreach (var definition in definitions.OfType<JsonObject>().Where(definition => GetText(definition["type"]) == "date"))
            replacement.Add(definition.DeepClone());

        if (!String.IsNullOrWhiteSpace(filter))
            Set(replacement, "keyword", JsonValue.Create(filter));

        return replacement;
    }

    /// <summary>
    /// Replaces every chip matching the type and term, preserving other criteria. Filter edits keep structured criteria
    /// before the search chip: filter terms AND together up to an OR, so "a OR b project:c" would not limit a to that project.
    /// </summary>
    public static void Set(JsonArray definitions, string type, JsonNode? value, bool hidden = false, string? term = null)
    {
        var definition = new JsonObject { ["type"] = type };
        if (term is not null)
            definition["term"] = term;
        definition["value"] = value;
        if (hidden)
            definition["hidden"] = true;

        int index = Find(definitions, type, term);
        if (index < 0)
        {
            int searchIndex = type == "keyword" ? -1 : Find(definitions, "keyword", term: null);
            index = searchIndex < 0 ? definitions.Count : searchIndex;
        }
        else
        {
            int duplicateIndex;
            while ((duplicateIndex = Find(definitions, type, term)) >= 0)
                definitions.RemoveAt(duplicateIndex);
        }

        definitions.Insert(index, definition);

        // Changing the time range must not change the meaning of an existing query.
        if (type != "date")
            MoveCriteriaBeforeSearch(definitions);
    }

    public static void Remove(JsonArray definitions, string type)
    {
        int index;
        while ((index = Find(definitions, type, term: null)) >= 0)
            definitions.RemoveAt(index);
    }

    public static string? ToFilter(JsonArray definitions)
    {
        string filter = String.Join(' ', definitions
            .OfType<JsonObject>()
            .Select(ToClause)
            .Where(clause => clause.Length > 0)).Trim();

        return filter.Length > 0 ? filter : null;
    }

    private static int Find(JsonArray definitions, string type, string? term)
    {
        for (int index = 0; index < definitions.Count; index++)
        {
            if (definitions[index] is JsonObject definition
                && GetText(definition["type"]) == type
                && (term is null || GetText(definition["term"]) == term))
            {
                return index;
            }
        }

        return -1;
    }

    private static void MoveCriteriaBeforeSearch(JsonArray definitions)
    {
        int searchIndex = Find(definitions, "keyword", term: null);
        if (searchIndex < 0)
            return;

        for (int index = searchIndex + 1; index < definitions.Count; index++)
        {
            if (definitions[index] is not JsonObject definition
                || GetText(definition["type"]) == "keyword"
                || ToClause(definition).Length == 0)
            {
                continue;
            }

            definitions.RemoveAt(index);
            definitions.Insert(searchIndex++, definition);
        }
    }

    private static string ToClause(JsonObject definition)
    {
        string text = GetText(definition["value"]) ?? String.Empty;
        return GetText(definition["type"]) switch
        {
            "boolean" or "number" => ToTermClause(definition, value => value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : value.ToJsonString()),
            "environment" => ToEnvironmentClause(GetValues(definition)),
            "keyword" => text.Trim(),
            "level" => ToAnyClause("level", GetValues(definition)),
            "project" => ToAnyClause("project", GetValues(definition)),
            "reference" => String.IsNullOrWhiteSpace(text) ? String.Empty : $"reference:{Quote(text)}",
            "session" => String.IsNullOrWhiteSpace(text) ? String.Empty : $"(reference:{Quote(text)} OR ref.session:{Quote(text)})",
            "status" => ToAnyClause("status", GetValues(definition)),
            "string" or "version" => ToTermClause(definition, value => Quote(GetText(value) ?? String.Empty)),
            "tag" => ToAnyClause("tag", GetValues(definition).Select(Quote).ToArray()),
            "type" => ToAnyClause("type", GetValues(definition)),
            // Dates are the view's time range rather than part of its filter, and the app ignores unknown types.
            _ => String.Empty
        };
    }

    private static string ToTermClause(JsonObject definition, Func<JsonNode, string> format)
    {
        if (GetText(definition["term"]) is not { } term)
            return String.Empty;

        return definition["value"] is { } value ? $"{term}:{format(value)}" : $"_missing_:{term}";
    }

    private static string ToAnyClause(string term, IReadOnlyCollection<string> values)
    {
        string[] clauses = values.Select(value => $"{term}:{value}").ToArray();
        return clauses.Length > 1 ? $"({String.Join(" OR ", clauses)})" : clauses.FirstOrDefault() ?? String.Empty;
    }

    private static string ToEnvironmentClause(IReadOnlyCollection<string> values)
    {
        string[] clauses = values
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Select(value => value.Length == 0 ? "_missing_:environment" : $"environment:{(SimpleEnvironmentRegex().IsMatch(value) ? value : EscapeAndQuote(value))}")
            .ToArray();

        return clauses.Length > 1 ? $"({String.Join(" OR ", clauses)})" : clauses.FirstOrDefault() ?? String.Empty;
    }

    private static string Quote(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.Length == 0 || (trimmed.Length > 1 && trimmed.StartsWith('"') && trimmed.EndsWith('"')))
            return trimmed;

        return SpecialCharacterRegex().IsMatch(trimmed) ? $"\"{trimmed}\"" : trimmed;
    }

    private static string EscapeAndQuote(string value) => $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static string[] GetValues(JsonObject definition)
        => definition["value"] is JsonArray values ? values.Select(GetText).OfType<string>().ToArray() : [];

    private static string? GetText(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out string? text) ? text : null;

    [GeneratedRegex("^[a-z0-9][a-z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SimpleEnvironmentRegex();

    [GeneratedRegex(@"[+\-&|!(){}\[\]^""~*?:\\/\s]", RegexOptions.CultureInvariant)]
    private static partial Regex SpecialCharacterRegex();
}
