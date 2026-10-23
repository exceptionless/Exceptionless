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
        else if (toolName == "count_events" && data["groups"] is JsonArray groups && data["group_by"]?.GetValue<string>() == "stack")
        {
            // Stack groups are keyed by stack id, so each one links to its stack like a search result does.
            foreach (var group in groups.OfType<JsonObject>())
            {
                if (group["key"]?.GetValue<string>() is { Length: > 0 } stackId)
                    group["webUrl"] = AssistantRoutes.Stack(stackId);
            }
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
            "get_event" or "get_stack_events" when item["stack_id"]?.GetValue<string>() is { Length: > 0 } stackId => AssistantRoutes.Event(stackId, id),
            "get_stack" or "search_stacks" => AssistantRoutes.Stack(id),
            "get_project_setup" => AssistantRoutes.ProjectConfigure(id),
            "list_projects" => AssistantRoutes.ProjectStacks(id),
            _ => null
        };

        if (webUrl is not null)
            item["webUrl"] = webUrl;
    }
}
