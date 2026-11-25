using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace Exceptionless.Web.Security;

internal sealed class SpaCspReportEndpoint(ILogger logger)
{
    public const string Path = "/csp-reports";

    private readonly Dictionary<string, string> _scripts = new(StringComparer.Ordinal)
    {
        ["/_app/env.js"] = "the SPA runtime configuration (env.js)",
        ["/_app/bootstrap.js"] = "the SPA bootstrap (bootstrap.js)"
    };

    public static void Map(WebApplication app)
    {
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var endpoint = new SpaCspReportEndpoint(loggerFactory.CreateLogger(nameof(SpaCspReportEndpoint)));
        endpoint.AddPublishedModules(app.Environment.WebRootFileProvider, "/_app/immutable");
        app.MapPost(Path, endpoint.ReceiveAsync)
            .AllowAnonymous()
            .WithMetadata(new HttpMethodMetadata([HttpMethods.Post], acceptCorsPreflight: true))
            .ExcludeFromDescription();
    }

    [DisableCors]
    [RequestSizeLimit(16 * 1024)]
    private async Task<IResult> ReceiveAsync(HttpRequest request)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType) ||
            !String.Equals(contentType.MediaType.Value, "application/csp-report", StringComparison.OrdinalIgnoreCase))
            return TypedResults.NoContent();

        ReportEnvelope? envelope;
        try
        {
            envelope = await JsonSerializer.DeserializeAsync<ReportEnvelope>(request.Body, cancellationToken: request.HttpContext.RequestAborted);
        }
        catch (BadHttpRequestException ex)
        {
            // Preserve Kestrel's rejection status without reporting it as an application error.
            return TypedResults.StatusCode(ex.StatusCode);
        }
        catch (JsonException)
        {
            return TypedResults.NoContent();
        }

        if (envelope?.Report is not { Disposition: "enforce", EffectiveDirective: "script-src" or "script-src-elem" } report ||
            !Uri.TryCreate(report.BlockedUri, UriKind.Absolute, out var blocked) || blocked.Scheme is not ("http" or "https") ||
            !Uri.TryCreate($"{request.Scheme}://{request.Host}", UriKind.Absolute, out var origin) ||
            Uri.Compare(blocked, origin, UriComponents.SchemeAndServer, UriFormat.UriEscaped, StringComparison.OrdinalIgnoreCase) != 0)
            return TypedResults.NoContent();

        if (!PathString.FromUriComponent(blocked).StartsWithSegments(request.PathBase, StringComparison.Ordinal, out var relativePath) ||
            relativePath.Value is not { } path || !_scripts.TryGetValue(path, out string? asset))
            return TypedResults.NoContent();

        string userAgent = request.Headers.UserAgent.ToString();
        using var scope = logger.BeginScope(new ExceptionlessState()
            .Tag("csp")
            .Property("BrowserUserAgent", userAgent.Length > 256 ? userAgent[..256] : userAgent));
        logger.LogWarning("Content Security Policy blocked {BlockedAsset:l}", asset);

        return TypedResults.NoContent();
    }

    private void AddPublishedModules(IFileProvider files, string directory)
    {
        foreach (var file in files.GetDirectoryContents(directory))
        {
            string path = $"{directory}/{file.Name}";
            if (file.IsDirectory)
                AddPublishedModules(files, path);
            else if (file.Name.EndsWith(".js", StringComparison.Ordinal))
                _scripts[path] = "a compiled SPA module";
        }
    }

    private sealed record ReportEnvelope([property: JsonPropertyName("csp-report")] Violation? Report);

    private sealed record Violation(
        [property: JsonPropertyName("disposition")] string? Disposition,
        [property: JsonPropertyName("effective-directive")] string? EffectiveDirective,
        [property: JsonPropertyName("blocked-uri")] string? BlockedUri);
}
