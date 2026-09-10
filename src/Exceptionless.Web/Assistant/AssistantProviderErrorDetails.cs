using System.Text.Json;
using System.Text.RegularExpressions;

namespace Exceptionless.Web.Assistant;

// Provider errors sometimes wrap the useful error in metadata.raw, and sometimes echo
// the request there too. Extract diagnostic fields instead of logging the response body.
internal sealed class AssistantProviderErrorDetails(JsonElement requestMessages, string? apiKey)
{
    private const int MaximumTextLength = 2048;
    private const int MaximumFields = 64;
    private const int MaximumItems = 16;
    private static readonly Regex s_credentials = new(
        @"\b(?:Bearer|Basic)\s+[^\s,;]+|\bsk-[A-Za-z0-9_-]+|\b(?:api[_-]?key|password|secret|access[_-]?token|authorization)\s*[:=]\s*[^\s,;]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
    private static readonly Regex s_urls = new(@"https?://[^\s<>""']+", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
    private readonly string[] _requestValues = GetRequestValues(requestMessages, apiKey).OrderByDescending(value => value.Length).ToArray();
    private int _fields;
    private int _remainingCharacters = 8192;

    public bool Truncated { get; private set; }
    public bool Redacted { get; private set; }

    public Dictionary<string, object?> Capture(JsonElement value, int depth = 0)
    {
        var result = new Dictionary<string, object?>();
        if (value.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in value.EnumerateObject())
        {
            if (_fields >= MaximumFields || depth >= 6 || _remainingCharacters <= 0)
            {
                Truncated = true;
                break;
            }

            switch (property.Name)
            {
                case "code": case "type": case "message": case "msg": case "param":
                case "error_type": case "provider_code": case "provider_error_code": case "provider_name":
                case "limit_source": case "is_byok": case "failed_routing_step": case "input_endpoint_count":
                case "reason": case "endpoint_count": case "request_id":
                case "requested": case "strategy": case "region": case "attempt": case "total":
                case "provider": case "model": case "status": case "selected":
                    _fields++;
                    result[property.Name] = CaptureScalar(property.Value);
                    break;
                case "error": case "metadata": case "endpoints":
                    _fields++;
                    result[property.Name] = Capture(property.Value, depth + 1);
                    break;
                case "raw": case "detail":
                    _fields++;
                    result[property.Name] = CaptureNested(property.Value, depth + 1);
                    break;
                case "errors": case "ineligibility_reasons": case "routing_funnel": case "attempts": case "available": case "loc":
                    _fields++;
                    result[property.Name] = CaptureArray(property.Value, depth + 1);
                    break;
                case "step":
                    _fields++;
                    result[property.Name] = CaptureScalar(property.Value);
                    break;
            }
        }

        return result;
    }

    public string SanitizeText(string value)
    {
        string sanitized;
        try
        {
            foreach (string requestValue in _requestValues)
            {
                value = requestValue.Length >= 8
                    ? value.Replace(requestValue, "[REDACTED]", StringComparison.Ordinal)
                    : Regex.Replace(value, $@"(?<!\w){Regex.Escape(requestValue)}(?!\w)", "[REDACTED]", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            }

            // Do not retain credentials or signed URLs embedded in an otherwise useful message.
            sanitized = s_urls.Replace(s_credentials.Replace(value, "[REDACTED]"), "[URL]");
        }
        catch (RegexMatchTimeoutException)
        {
            // Diagnostic filtering must never replace the original provider failure.
            sanitized = "[REDACTED]";
        }

        Redacted |= sanitized.Contains("[REDACTED]", StringComparison.Ordinal) || sanitized.Contains("[URL]", StringComparison.Ordinal);
        sanitized = String.Concat(sanitized.Select(character => Char.IsControl(character) ? ' ' : character));
        int length = Math.Min(MaximumTextLength, _remainingCharacters);
        if (sanitized.Length > length)
        {
            Truncated = true;
            sanitized = sanitized[..length];
        }

        _remainingCharacters -= sanitized.Length;
        return sanitized;
    }

    private object? CaptureScalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => SanitizeText(value.GetString()!),
        JsonValueKind.Number when value.TryGetDecimal(out decimal number) => number,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };

    private object? CaptureNested(JsonElement value, int depth)
    {
        if (depth >= 6)
        {
            Truncated = true;
            return null;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            return Capture(value, depth);
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return CaptureArray(value, depth);
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            return CaptureScalar(value);
        }

        string text = value.GetString()!;
        if (text.TrimStart().StartsWith('{') || text.TrimStart().StartsWith('['))
        {
            try
            {
                using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 });
                return CaptureNested(document.RootElement, depth + 1);
            }
            catch (JsonException)
            {
                // A partial JSON body cannot be safely filtered by field name.
                return "[INVALID JSON]";
            }
        }

        return SanitizeText(text);
    }

    private object? CaptureArray(JsonElement value, int depth)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        Truncated |= value.GetArrayLength() > MaximumItems;
        return value.EnumerateArray().Take(MaximumItems).Select(item => CaptureNested(item, depth)).ToArray();
    }

    private static HashSet<string> GetRequestValues(JsonElement messages, string? apiKey)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        if (!String.IsNullOrEmpty(apiKey))
        {
            values.Add(apiKey);
        }

        if (messages.ValueKind == JsonValueKind.Array)
        {
            foreach (var message in messages.EnumerateArray())
            {
                if (message.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                {
                    AddValue(content.GetString()!, values);
                }

                foreach (string name in new[] { "reasoning", "reasoning_content", "reasoning_details" })
                {
                    if (message.TryGetProperty(name, out var reasoning))
                    {
                        AddJsonValues(reasoning, values);
                    }
                }

                if (message.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
                {
                    foreach (var call in calls.EnumerateArray())
                    {
                        if (call.TryGetProperty("function", out var function) && function.TryGetProperty("arguments", out var arguments)
                            && arguments.ValueKind == JsonValueKind.String)
                        {
                            AddValue(arguments.GetString()!, values);
                        }
                    }
                }
            }
        }

        return values;
    }

    private static void AddValue(string value, HashSet<string> values)
    {
        if (!String.IsNullOrEmpty(value))
        {
            values.Add(value);
        }

        foreach (string line in value.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length >= 8)
            {
                values.Add(line);
            }
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            AddJsonValues(document.RootElement, values);
        }
        catch (JsonException)
        {
        }
    }

    private static void AddJsonValues(JsonElement value, HashSet<string> values)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                AddJsonValues(property.Value, values);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                AddJsonValues(item, values);
            }
        }
        else if (value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text)
        {
            values.Add(text);
        }
    }
}
