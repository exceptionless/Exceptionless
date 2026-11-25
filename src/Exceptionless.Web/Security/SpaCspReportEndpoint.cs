using System.Text.Json;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using ExceptionlessLogLevel = Exceptionless.Logging.LogLevel;

namespace Exceptionless.Web.Security;

internal static class SpaCspReportEndpoint
{
    public const string Path = "/csp-reports";

    public static void MapSpaCspReportEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Path, ReceiveAsync)
            .AllowAnonymous()
            .WithMetadata(new HttpMethodMetadata([HttpMethods.Post], acceptCorsPreflight: true))
            .ExcludeFromDescription();
    }

    [DisableCors]
    [RequestSizeLimit(16 * 1024)]
    private static async Task<IResult> ReceiveAsync(HttpRequest request, ILoggerFactory loggerFactory)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType) ||
            !String.Equals(contentType.MediaType.Value, "application/csp-report", StringComparison.OrdinalIgnoreCase))
            return TypedResults.NoContent();

        string? asset;
        try
        {
            using var report = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
            asset = GetBlockedAsset(report.RootElement, request.Host);
        }
        catch (BadHttpRequestException ex)
        {
            // Preserve Kestrel's rejection status without reporting it as an application error.
            return TypedResults.StatusCode(ex.StatusCode);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return TypedResults.NoContent();
        }

        if (asset is null)
            return TypedResults.NoContent();

        var client = request.HttpContext.RequestServices.GetService<ExceptionlessClient>();
        if (client is { Configuration.Enabled: true })
        {
            string userAgent = request.Headers.UserAgent.ToString();
            client.CreateLog(nameof(SpaCspReportEndpoint), $"Content Security Policy blocked {asset}", ExceptionlessLogLevel.Warn)
                // Keep the SDK from enriching this anonymous report with the ambient request or user.
                .SetHttpContext(new DefaultHttpContext())
                .SetProperty("Blocked Asset", asset)
                .SetProperty("Browser User Agent", userAgent.Length > 256 ? userAgent[..256] : userAgent)
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
            !Uri.TryCreate(GetString(report, "blocked-uri"), UriKind.Absolute, out var blocked) || blocked.Scheme is not ("http" or "https") ||
            !HostString.FromUriComponent(blocked).Equals(host))
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
