using System.Text.Json;
using ExceptionlessLogLevel = Exceptionless.Logging.LogLevel;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Exceptionless.Web.Api.Endpoints;

/// <summary>
/// Receives browser CSP violation reports and submits them to Exceptionless, so a blocked script or
/// connection is noticed instead of silently breaking the app. Browsers send these reports even when
/// the blocked script stops the app (and its Exceptionless client) from booting.
/// </summary>
public static class CspReportEndpoints
{
    public const string ReportPath = "/api/v2/csp-reports";

    // Real reports are ~1-3 KB; anything larger is not a browser report.
    private const int MaxReportBytes = 16 * 1024;
    private const int MaxFieldLength = 512;

    // Browser extensions inject scripts that our policy blocks; those reports are noise.
    private static readonly string[] _extensionSchemes = ["chrome-extension", "moz-extension", "safari-extension", "safari-web-extension", "ms-browser-extension"];

    public static IEndpointRouteBuilder MapCspReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Browsers POST anonymously to report-uri with application/csp-report; ThrottlingMiddleware
        // rate limits these by client IP like any other anonymous API request.
        endpoints.MapPost(ReportPath, async (HttpContext context, ILoggerFactory loggerFactory) =>
        {
            var violation = Parse(await ReadReportAsync(context.Request));
            if (violation is null || violation.IsIgnored)
                return HttpResults.NoContent();

            string? userAgent = Truncate(context.Request.Headers.UserAgent.ToString());
            var client = context.RequestServices.GetService<ExceptionlessClient>();
            if (client is { Configuration.Enabled: true })
            {
                var builder = client.CreateLog(nameof(CspReportEndpoints), violation.Title, ExceptionlessLogLevel.Warn)
                    .AddTags("csp")
                    .SetManualStackingInfo(violation.Title, new Dictionary<string, string>
                    {
                        ["Directive"] = violation.Directive,
                        ["Blocked"] = violation.Blocked
                    });

                foreach (var (name, value) in violation.GetProperties(userAgent))
                    builder.SetProperty(name, value);

                builder.Submit();
            }
            else
            {
                // Self-hosted installs without Exceptionless configured still see violations in their logs.
                loggerFactory.CreateLogger(typeof(CspReportEndpoints).FullName!).LogWarning(
                    "{Title} on {DocumentUri} (source {SourceFile}:{LineNumber}, {Disposition}) {UserAgent}",
                    violation.Title, violation.DocumentUri, violation.SourceFile, violation.LineNumber, violation.Disposition, userAgent);
            }

            return HttpResults.NoContent();
        })
        .AllowAnonymous()
        .DisableAntiforgery()
        .ExcludeFromDescription();

        return endpoints;
    }

    internal static CspViolation? Parse(JsonElement? report)
    {
        if (report is not { ValueKind: JsonValueKind.Object } root ||
            !root.TryGetProperty("csp-report", out var body) || body.ValueKind is not JsonValueKind.Object)
            return null;

        string directive = GetString(body, "effective-directive") ?? GetString(body, "violated-directive") ?? "unknown";
        string? blockedUri = StripQuery(GetString(body, "blocked-uri"));
        string? sourceFile = StripQuery(GetString(body, "source-file"));
        int? lineNumber = body.TryGetProperty("line-number", out var line) && line.ValueKind is JsonValueKind.Number && line.TryGetInt32(out int number) ? number : null;

        // Keywords (inline, eval, wasm-eval) say nothing about where; stack them by the script that tried.
        string blocked = String.IsNullOrEmpty(blockedUri) ? "unknown" : blockedUri;
        if (!blocked.Contains("://", StringComparison.Ordinal) && !String.IsNullOrEmpty(sourceFile))
            blocked = $"{blocked} from {sourceFile}";

        return new CspViolation(
            Directive: directive,
            Blocked: blocked,
            BlockedUri: blockedUri,
            DocumentUri: StripQuery(GetString(body, "document-uri")),
            SourceFile: sourceFile,
            LineNumber: lineNumber,
            Disposition: GetString(body, "disposition"));
    }

    private static async Task<JsonElement?> ReadReportAsync(HttpRequest request)
    {
        if (request.ContentLength is > MaxReportBytes)
            return null;

        try
        {
            // Read at most one byte past the cap so chunked bodies without Content-Length stay bounded.
            byte[] buffer = new byte[MaxReportBytes + 1];
            int length = 0;
            int read;
            while (length < buffer.Length && (read = await request.Body.ReadAsync(buffer.AsMemory(length), request.HttpContext.RequestAborted)) > 0)
                length += read;

            if (length is 0 or > MaxReportBytes)
                return null;

            using var document = JsonDocument.Parse(buffer.AsMemory(0, length));
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String
            ? Truncate(value.GetString())
            : null;
    }

    // Report URLs keep their query strings, which can carry tokens (e.g. password reset links) and
    // cache-busting versions that would split one problem into many stacks.
    private static string? StripQuery(string? value)
    {
        if (String.IsNullOrEmpty(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return value;

        return uri.GetLeftPart(UriPartial.Path);
    }

    private static string? Truncate(string? value)
    {
        return value is { Length: > MaxFieldLength } ? value[..MaxFieldLength] : value;
    }

    internal sealed record CspViolation(string Directive, string Blocked, string? BlockedUri, string? DocumentUri, string? SourceFile, int? LineNumber, string? Disposition)
    {
        public string Title => $"CSP blocked {Blocked} ({Directive})";

        public bool IsIgnored => IsFromExtension || IsKnownBenign;

        public IEnumerable<(string Name, object Value)> GetProperties(string? userAgent)
        {
            (string Name, object? Value)[] properties =
            [
                ("Directive", Directive), ("Blocked Uri", BlockedUri), ("Document Uri", DocumentUri), ("Source File", SourceFile),
                ("Line Number", LineNumber), ("Disposition", Disposition), ("User Agent", userAgent)
            ];

            return properties.Where(property => property.Value is not null && property.Value is not "").Select(property => (property.Name, property.Value!));
        }

        private bool IsFromExtension => _extensionSchemes.Any(scheme =>
            (BlockedUri?.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (SourceFile?.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) ?? false));

        // Scalar bundles Zod 4, which probes for eval support with try { Function('') } and falls back.
        // Our own app avoids the probe with Zod's jitless config (hooks.client.ts).
        private bool IsKnownBenign => String.Equals(BlockedUri, "eval", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(SourceFile, UriKind.Absolute, out var source)
            && String.Equals(source.AbsolutePath, "/docs/scalar.js", StringComparison.OrdinalIgnoreCase);
    }
}
