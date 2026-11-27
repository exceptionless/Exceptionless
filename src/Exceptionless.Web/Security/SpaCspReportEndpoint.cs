using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Exceptionless.Web.Security;

internal static class SpaCspReportEndpoint
{
    public const string Path = "/csp-reports";

    public static void Map(IEndpointRouteBuilder endpoints)
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

        if (!PathString.FromUriComponent(blocked).StartsWithSegments(request.PathBase.Add("/_app"), StringComparison.Ordinal))
            return TypedResults.NoContent();

        var logger = loggerFactory.CreateLogger(nameof(SpaCspReportEndpoint));
        using var scope = logger.BeginScope(new ExceptionlessState().Tag("csp"));
        logger.LogWarning("Content Security Policy blocked a SPA script");

        return TypedResults.NoContent();
    }

    private sealed record ReportEnvelope([property: JsonPropertyName("csp-report")] Violation? Report);

    private sealed record Violation(
        [property: JsonPropertyName("disposition")] string? Disposition,
        [property: JsonPropertyName("effective-directive")] string? EffectiveDirective,
        [property: JsonPropertyName("blocked-uri")] string? BlockedUri);
}
