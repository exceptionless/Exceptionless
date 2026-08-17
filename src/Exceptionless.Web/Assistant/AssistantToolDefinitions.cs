using System.Reflection;
using System.Text.Json.Nodes;
using Exceptionless.Web.Mcp;
using ModelContextProtocol.Server;

namespace Exceptionless.Web.Assistant;

internal static class AssistantToolDefinitions
{
    public const string SuggestFollowupsToolName = "suggest_followups";

    private static readonly string[] s_methodNames =
    [
        nameof(ExceptionlessMcpTools.GetEventAsync),
        nameof(ExceptionlessMcpTools.GetProjectSetupAsync),
        nameof(ExceptionlessMcpTools.GetStackAsync),
        nameof(ExceptionlessMcpTools.ListProjectsAsync),
        nameof(ExceptionlessMcpTools.SearchStacksAsync),
        nameof(ExceptionlessMcpTools.UpdateStackStatusAsync),
        nameof(ExceptionlessMcpTools.SnoozeStackAsync),
        nameof(ExceptionlessMcpTools.SetStackCriticalAsync),
        nameof(ExceptionlessMcpTools.AddStackReferenceLinkAsync),
        nameof(ExceptionlessMcpTools.RemoveStackReferenceLinkAsync)
    ];

    public static object[] Create(ExceptionlessMcpTools tools, AssistantChatRequest request)
    {
        string? currentEventId = AssistantService.GetRouteValue(request.Path, "event");
        string? currentStackId = AssistantService.GetRouteValue(request.Path, "stack");

        var definitions = s_methodNames.Select(methodName =>
        {
            MethodInfo method = typeof(ExceptionlessMcpTools).GetMethod(methodName)
                ?? throw new InvalidOperationException($"Could not find MCP tool method {methodName}.");
            var protocolTool = McpServerTool.Create(method, tools, new McpServerToolCreateOptions()).ProtocolTool;
            var schema = JsonNode.Parse(protocolTool.InputSchema.GetRawText())?.AsObject()
                ?? throw new InvalidOperationException($"MCP tool {protocolTool.Name} has no input schema.");

            if (protocolTool.Name == "get_event" && currentEventId is not null)
                ApplyCurrentPageDefault(schema, "eventId", "Defaults to the current page event id when omitted.");

            if (protocolTool.Name is "get_stack" or "update_stack_status" or "snooze_stack" or "set_stack_critical" or "add_stack_reference_link" or "remove_stack_reference_link"
                && currentStackId is not null)
            {
                ApplyCurrentPageDefault(schema, "stackId", "Defaults to the current page stack id when omitted.");
            }

            if (request.ProjectId is not null)
                ApplyCurrentPageDefault(schema, "projectId", "Defaults to the current page project id when omitted. Only specify another project when the user explicitly requests a broader or different scope.");

            if (protocolTool.Name is "list_projects" or "search_stacks")
                ApplyMaximum(schema, "limit", AssistantLimits.MaximumToolItemsPerCall);

            if (protocolTool.Name == "get_event")
                ApplyMaximum(schema, "maxDetailSize", AssistantLimits.MaximumEventDetailCharacters);

            return new
            {
                type = "function",
                function = new
                {
                    name = protocolTool.Name,
                    description = protocolTool.Description,
                    parameters = schema
                }
            };
        }).ToList<object>();

        definitions.Add(new
        {
            type = "function",
            function = new
            {
                name = SuggestFollowupsToolName,
                description = "Supplies useful follow-up prompt buttons with a complete final answer. Call this whenever the answer asks what the user wants to investigate or do next, or offers two or more concrete follow-up choices. Omit it when no next step is genuinely useful. Do not call it before required data tools or in the same response as another tool.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        actions = new
                        {
                            type = "array",
                            description = "One to three distinct next messages the user may want to send. When the answer offers concrete choices, represent up to the three best choices here instead of leaving them only in prose.",
                            minItems = 1,
                            maxItems = AssistantLimits.MaximumSuggestedActions,
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    label = new
                                    {
                                        type = "string",
                                        description = "A concise two-to-five-word button label.",
                                        maxLength = AssistantLimits.MaximumSuggestedActionLabelCharacters
                                    },
                                    prompt = new
                                    {
                                        type = "string",
                                        description = "The complete follow-up message to send when the button is selected.",
                                        maxLength = AssistantLimits.MaximumSuggestedActionPromptCharacters
                                    },
                                    href = new
                                    {
                                        type = "string",
                                        description = "An internal Exceptionless path to open instead of sending a prompt. Only use the current project's Client Setup path supplied by get_project_setup.",
                                        maxLength = 512
                                    }
                                },
                                oneOf = new object[]
                                {
                                    new { required = new[] { "prompt" } },
                                    new { required = new[] { "href" } }
                                },
                                required = new[] { "label" },
                                additionalProperties = false
                            }
                        }
                    },
                    required = new[] { "actions" },
                    additionalProperties = false
                }
            }
        });

        return definitions.ToArray();
    }

    private static void ApplyMaximum(JsonObject schema, string propertyName, int maximum)
    {
        if (schema["properties"]?[propertyName] is JsonObject property)
            property["maximum"] = maximum;
    }

    private static void ApplyCurrentPageDefault(JsonObject schema, string propertyName, string description)
    {
        if (schema["required"] is JsonArray required)
        {
            for (int index = required.Count - 1; index >= 0; index--)
            {
                if (String.Equals(required[index]?.GetValue<string>(), propertyName, StringComparison.Ordinal))
                    required.RemoveAt(index);
            }
        }

        if (schema["properties"]?[propertyName] is JsonObject property)
        {
            string? existingDescription = property["description"]?.GetValue<string>();
            property["description"] = String.IsNullOrWhiteSpace(existingDescription)
                ? description
                : $"{existingDescription} {description}";
        }
    }
}
