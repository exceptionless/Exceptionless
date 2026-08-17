using System.Text.Json;

namespace Exceptionless.Web.Assistant;

internal static class AssistantSuggestedActionParser
{
    private const string ConfigureProjectFallbackPrompt = "How do I configure this project to start sending events?";

    public static IReadOnlyCollection<AssistantSuggestedAction> Parse(IEnumerable<string> toolArguments, string? configureHref)
    {
        var actions = new List<AssistantSuggestedAction>();
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

    public static string? GetProjectSetupHref(string result)
    {
        try
        {
            using var document = JsonDocument.Parse(result);
            var root = document.RootElement;
            if (!root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True
                || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? projectId = GetString(data, "id")?.Trim();
            string? webUrl = GetString(data, "webUrl")?.Trim();
            if (String.IsNullOrWhiteSpace(projectId) || String.IsNullOrWhiteSpace(webUrl))
                return null;

            string expectedHref = AssistantRoutes.ProjectConfigure(projectId);
            return String.Equals(webUrl, expectedHref, StringComparison.Ordinal) ? expectedHref : null;
        }
        catch (JsonException)
        {
            return null;
        }
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
        string actionPrompt = hasPrompt ? Truncate(prompt!, AssistantLimits.MaximumSuggestedActionPromptCharacters) : ConfigureProjectFallbackPrompt;

        return new AssistantSuggestedAction(label, actionPrompt, hasHref ? href : null);
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
