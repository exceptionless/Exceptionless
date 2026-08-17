using System.Text.Json;
using System.Text.Json.Nodes;

namespace Exceptionless.Web.Assistant;

internal static class AssistantToolResultSerializer
{
    public static string Serialize(string toolName, object result, JsonSerializerOptions serializerOptions)
    {
        var root = JsonSerializer.SerializeToNode(result, serializerOptions);
        if (root?["data"] is not JsonObject data)
        {
            return root?.ToJsonString(serializerOptions) ?? "null";
        }

        if (data["items"] is JsonArray items)
        {
            foreach (var item in items.OfType<JsonObject>())
                AddWebUrl(toolName, item);
        }
        else
        {
            AddWebUrl(toolName, data);
        }

        return root.ToJsonString(serializerOptions);
    }

    private static void AddWebUrl(string toolName, JsonObject item)
    {
        string? id = item["id"]?.GetValue<string>();
        if (String.IsNullOrWhiteSpace(id))
        {
            return;
        }

        string? webUrl = toolName switch
        {
            "get_event" when item["stack_id"]?.GetValue<string>() is { Length: > 0 } stackId => AssistantRoutes.Event(stackId, id),
            "get_stack" or "search_stacks" => AssistantRoutes.Stack(id),
            "get_project_setup" => AssistantRoutes.ProjectConfigure(id),
            "list_projects" => AssistantRoutes.ProjectStacks(id),
            _ => null
        };

        if (webUrl is not null)
            item["webUrl"] = webUrl;
    }
}
