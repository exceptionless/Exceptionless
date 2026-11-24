using System.Text.Json;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Foundatio.Caching;
using Microsoft.AspNetCore.Cors;
using Microsoft.Net.Http.Headers;

namespace Exceptionless.Web.Api.Endpoints;

internal static class SpaCspReportEndpoint
{
    public const string Path = "/api/v2/csp-reports";
    private const int MaxReportBytes = 16 * 1024;

    public static void MapSpaCspReportEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Path, ReceiveAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithMetadata(new DisableCorsAttribute(), new HttpMethodMetadata([HttpMethods.Post], acceptCorsPreflight: true))
            .ExcludeFromDescription();
    }

    private static async Task<IResult> ReceiveAsync(HttpRequest request, ICacheClient cache, ILoggerFactory loggerFactory)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType) ||
            !String.Equals(contentType.MediaType.Value, "application/csp-report", StringComparison.OrdinalIgnoreCase) ||
            request.ContentLength is > MaxReportBytes)
            return TypedResults.NoContent();

        // Bound chunked requests too. One extra byte distinguishes a full report from a truncated one.
        byte[] buffer = new byte[MaxReportBytes + 1];
        int length = await request.Body.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, request.HttpContext.RequestAborted);
        if (length is 0 or > MaxReportBytes)
            return TypedResults.NoContent();

        string? asset;
        try
        {
            using var report = JsonDocument.Parse(buffer.AsMemory(0, length));
            asset = GetBlockedAsset(report.RootElement, request.Host);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return TypedResults.NoContent();
        }

        if (asset is null)
            return TypedResults.NoContent();

        // A widespread startup failure can produce a report on every page load.
        try
        {
            if (!await cache.AddAsync($"spa-csp-report:{asset}", true, TimeSpan.FromMinutes(1)))
                return TypedResults.NoContent();
        }
        catch (Exception) when (!request.HttpContext.RequestAborted.IsCancellationRequested)
        {
            // Drop reports while the cache is unavailable to avoid an unbounded event flood.
            return TypedResults.NoContent();
        }

        string title = $"Content Security Policy blocked {asset}";
        var client = request.HttpContext.RequestServices.GetService<ExceptionlessClient>();
        if (client is { Configuration.Enabled: true })
        {
            string userAgent = request.Headers.UserAgent.ToString();
            client.CreateEvent()
                // Keep the SDK from enriching this anonymous report with the ambient request or user.
                .SetHttpContext(new DefaultHttpContext())
                .SetType(Event.KnownTypes.Error)
                .SetMessage(title)
                .SetProperty(Event.KnownDataKeys.SimpleError, new SimpleError { Type = "ContentSecurityPolicyViolation", Message = title })
                .SetProperty("Browser User Agent", userAgent.Length > 256 ? userAgent[..256] : userAgent)
                .SetManualStackingInfo(title, new Dictionary<string, string> { ["CSP SPA asset"] = asset })
                .AddTags("csp")
                .Submit();
        }
        else
        {
            loggerFactory.CreateLogger(typeof(SpaCspReportEndpoint).FullName!).LogWarning("Content Security Policy blocked {Asset}", asset);
        }

        return TypedResults.NoContent();
    }

    private static string? GetBlockedAsset(JsonElement root, HostString host)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("csp-report", out var report) || report.ValueKind != JsonValueKind.Object ||
            GetString(report, "disposition") != "enforce" || GetString(report, "effective-directive") is not ("script-src" or "script-src-elem") ||
            !Uri.TryCreate(GetString(report, "document-uri"), UriKind.Absolute, out var document) || document.Scheme is not ("http" or "https") ||
            !String.Equals(document.Host, host.Host, StringComparison.OrdinalIgnoreCase) || document.Port != (host.Port ?? (document.Scheme == "https" ? 443 : 80)) ||
            !Uri.TryCreate(GetString(report, "blocked-uri"), UriKind.Absolute, out var blocked) ||
            Uri.Compare(document, blocked, UriComponents.SchemeAndServer, UriFormat.UriEscaped, StringComparison.OrdinalIgnoreCase) != 0)
            return null;

        // Group hashed modules across builds and keep all reported URLs and tokens out of events.
        return blocked.AbsolutePath switch
        {
            "/_app/env.js" => "the SPA runtime configuration (env.js)",
            "/_app/bootstrap.js" => "the SPA bootstrap (bootstrap.js)",
            var path when path.StartsWith("/_app/immutable/", StringComparison.Ordinal) && path.EndsWith(".js", StringComparison.Ordinal) => "a compiled SPA module",
            _ => null
        };
    }

    private static string? GetString(JsonElement report, string name) =>
        report.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
