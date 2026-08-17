using System.Text.Json;

namespace Exceptionless.Web.Assistant;

internal static class AssistantSuggestedActionParser
{
    public static IReadOnlyCollection<AssistantSuggestedAction> Parse(IEnumerable<string> toolArguments, string? projectId)
    {
        var actions = new List<AssistantSuggestedAction>();
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? configureHref = String.IsNullOrWhiteSpace(projectId) ? null : AssistantRoutes.ProjectConfigure(projectId);

        foreach (string arguments in toolArguments)
        {
            using var document = ParseArguments(arguments);
            if (!document.RootElement.TryGetProperty("actions", out var actionItems) || actionItems.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var actionItem in actionItems.EnumerateArray())
            {
                var action = ParseAction(actionItem, configureHref);
                if (action is null || !destinations.Add(GetDestination(action)))
                    continue;

                actions.Add(action);
                if (actions.Count >= AssistantLimits.MaximumSuggestedActions)
                    return actions;
            }
        }

        return actions;
    }

    private static AssistantSuggestedAction? ParseAction(JsonElement item, string? configureHref)
    {
        string? label = GetString(item, "label")?.Trim();
        string? prompt = GetString(item, "prompt")?.Trim();
        string? href = GetString(item, "href")?.Trim();
        bool hasPrompt = !String.IsNullOrWhiteSpace(prompt);
        bool hasHref = !String.IsNullOrWhiteSpace(href);
        if (String.IsNullOrWhiteSpace(label) || hasPrompt == hasHref)
            return null;

        if (hasHref && !String.Equals(href, configureHref, StringComparison.Ordinal))
            return null;

        label = String.Join(' ', label.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        label = Truncate(label, AssistantLimits.MaximumSuggestedActionLabelCharacters);
        prompt = hasPrompt ? Truncate(prompt!, AssistantLimits.MaximumSuggestedActionPromptCharacters) : null;

        return new AssistantSuggestedAction(label, prompt, hasHref ? href : null);
    }

    private static string GetDestination(AssistantSuggestedAction action)
        => action.Prompt is not null ? $"prompt:{action.Prompt}" : $"href:{action.Href}";

    private static string Truncate(string value, int maximumLength)
        => value.Length > maximumLength ? value[..maximumLength].TrimEnd() : value;

    private static JsonDocument ParseArguments(string arguments)
    {
        try
        {
            return JsonDocument.Parse(String.IsNullOrWhiteSpace(arguments) ? "{}" : arguments);
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}");
        }
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
