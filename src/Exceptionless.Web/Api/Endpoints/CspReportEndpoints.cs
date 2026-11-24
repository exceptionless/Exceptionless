using System.Text.Json;
using Exceptionless.Web.Extensions;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Exceptionless.Web.Api.Endpoints;

/// <summary>
/// Receives browser CSP violation reports so a blocked script or connection shows up in our logs
/// instead of silently breaking the app (e.g. a browser update changing how a directive applies).
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
            var report = await ReadReportAsync(context.Request);
            if (report is not null && !IsFromExtension(report) && !IsKnownBenign(report))
            {
                var logger = loggerFactory.CreateLogger(typeof(CspReportEndpoints).FullName!);
                logger.LogWarning(
                    "CSP violation: {EffectiveDirective} blocked {BlockedUri} on {DocumentUri} (source {SourceFile}:{LineNumber}, {Disposition}) from {ClientIpAddress} {UserAgent}",
                    report.EffectiveDirective, report.BlockedUri, report.DocumentUri, report.SourceFile, report.LineNumber,
                    report.Disposition, context.Request.GetClientIpAddress(), Truncate(context.Request.Headers.UserAgent.ToString()));
            }

            return HttpResults.NoContent();
        })
        .AllowAnonymous()
        .DisableAntiforgery()
        .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<CspViolation?> ReadReportAsync(HttpRequest request)
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
            if (document.RootElement.ValueKind is not JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("csp-report", out var body) || body.ValueKind is not JsonValueKind.Object)
                return null;

            return new CspViolation(
                GetString(body, "effective-directive") ?? GetString(body, "violated-directive"),
                GetString(body, "blocked-uri"),
                GetString(body, "document-uri"),
                GetString(body, "source-file"),
                body.TryGetProperty("line-number", out var line) && line.ValueKind is JsonValueKind.Number && line.TryGetInt32(out int lineNumber) ? lineNumber : null,
                GetString(body, "disposition"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsFromExtension(CspViolation report)
    {
        return _extensionSchemes.Any(scheme =>
            (report.BlockedUri?.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (report.SourceFile?.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    // Scalar bundles Zod 4, which probes for eval support with try { Function('') } and falls back.
    // Our own app avoids the probe with Zod's jitless config (hooks.client.ts).
    private static bool IsKnownBenign(CspViolation report)
    {
        return String.Equals(report.BlockedUri, "eval", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(report.SourceFile, UriKind.Absolute, out var source)
            && String.Equals(source.AbsolutePath, "/docs/scalar.js", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String
            ? Truncate(value.GetString())
            : null;
    }

    private static string? Truncate(string? value)
    {
        return value is { Length: > MaxFieldLength } ? value[..MaxFieldLength] : value;
    }

    private sealed record CspViolation(string? EffectiveDirective, string? BlockedUri, string? DocumentUri, string? SourceFile, int? LineNumber, string? Disposition);
}
