using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Exceptionless.Web.Assistant;

/// <summary>
/// Recovers DeepSeek tool calls that a provider returned as DSML text instead of structured
/// tool_calls. Some OpenRouter providers skip DeepSeek's tool parser, which leaves markup like
/// <c>&lt;｜DSML｜tool_calls&gt;&lt;｜DSML｜invoke name="search_stacks"&gt;…</c> in the content.
/// </summary>
/// <remarks>Mirrors vLLM's DeepSeek V4 and V4.1 parsers: string="true" values are literal text, and other values are JSON with a literal fallback.</remarks>
internal static class AssistantDsmlToolCallParser
{
    // Providers have been seen emitting full-width, ASCII, and doubled pipes around DSML, and V4.1 adds a space after it.
    private const string Tag = @"\s*[|｜]{1,2}\s*DSML\s*[|｜]{1,2}\s*";
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(1);
    private static readonly Regex s_markupStart = new($@"<{Tag}(?:(?:tool_?|function_)?calls|tool|invoke)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, s_timeout);
    private static readonly Regex s_invoke = new($@"<{Tag}invoke\s+name=""([^""]+)""\s*>(.*?)</{Tag}invoke\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline, s_timeout);
    private static readonly Regex s_parameter = new($@"<{Tag}parameter\s+name=""([^""]+)""(?:\s+string=""([^""]*)"")?\s*>(.*?)</{Tag}parameter\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline, s_timeout);
    private static readonly Regex s_markupRemainder = new($@"</?{Tag}[a-z_]+\b[^>]*>|\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, s_timeout);
    private static readonly Regex s_closingWrapper = new($@"</{Tag}(?:(?:tool_?|function_)?calls|tool)\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, s_timeout);
    private static readonly Regex s_tagStart = new(@"<\s*/?\s*[|｜]", RegexOptions.CultureInvariant, s_timeout);

    /// <summary>Parses complete DSML tool calls from provider content.</summary>
    /// <param name="content">The full provider message content.</param>
    /// <param name="leadingText">Answer text that preceded the tool calls.</param>
    /// <param name="toolCalls">The recovered tool names and JSON arguments, in order.</param>
    /// <returns><see langword="false"/> when no complete tool call is found or the calls are interleaved with other text.</returns>
    public static bool TryParse(string content, out string leadingText, out IReadOnlyList<(string Name, string Arguments)> toolCalls)
    {
        leadingText = String.Empty;
        toolCalls = [];

        try
        {
            var start = s_markupStart.Match(content);
            if (!start.Success)
                return false;

            string markup = content[start.Index..];
            var calls = new List<(string Name, string Arguments)>();
            int consumed = 0;
            foreach (Match invoke in s_invoke.Matches(markup))
            {
                // Anything between calls other than wrapper tags means the markup is not just tool calls.
                if (!IsOnlyMarkup(markup[consumed..invoke.Index]))
                    return false;

                if (!TryParseArguments(invoke.Groups[2].Value, out string arguments))
                    return false;

                calls.Add((invoke.Groups[1].Value.Trim(), arguments));
                consumed = invoke.Index + invoke.Length;
            }

            // Like vLLM, text after the tool calls is dropped because a tool call ends the response. Leftover markup
            // means a later call was cut off, and running only the calls before it could apply part of a change.
            if (calls.Count == 0 || calls.Any(call => call.Name.Length == 0) || s_tagStart.IsMatch(s_closingWrapper.Replace(markup[consumed..], String.Empty)))
                return false;

            leadingText = content[..start.Index].TrimEnd();
            toolCalls = calls;
            return true;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool TryParseArguments(string body, out string arguments)
    {
        var values = new JsonObject();
        int consumed = 0;
        foreach (Match parameter in s_parameter.Matches(body))
        {
            if (!String.IsNullOrWhiteSpace(body[consumed..parameter.Index]))
            {
                arguments = String.Empty;
                return false;
            }

            string value = parameter.Groups[3].Value;
            values[parameter.Groups[1].Value] = String.Equals(parameter.Groups[2].Value, "true", StringComparison.OrdinalIgnoreCase)
                ? JsonValue.Create(value)
                : ParseJsonValue(value);
            consumed = parameter.Index + parameter.Length;
        }

        if (!String.IsNullOrWhiteSpace(body[consumed..]))
        {
            arguments = String.Empty;
            return false;
        }

        arguments = values.ToJsonString();
        return true;
    }

    private static JsonNode? ParseJsonValue(string value)
    {
        try
        {
            return JsonNode.Parse(value);
        }
        catch (JsonException)
        {
            return JsonValue.Create(value);
        }
    }

    private static bool IsOnlyMarkup(string text) => s_markupRemainder.Replace(text, String.Empty).Length == 0;
}
