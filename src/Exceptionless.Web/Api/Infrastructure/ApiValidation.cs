using Microsoft.AspNetCore.Http.Features;

namespace Exceptionless.Web.Api.Infrastructure;

public static class ApiValidation
{
    public static IResult MissingRequestBody()
    {
        return global::Microsoft.AspNetCore.Http.Results.ValidationProblem(
            new Dictionary<string, string[]> { [String.Empty] = ["A non-empty request body is required."] },
            statusCode: StatusCodes.Status400BadRequest);
    }

    public static IResult? ValidateJsonContentType(HttpRequest request)
    {
        return ValidateContentType(request, "application/json");
    }

    public static IResult? ValidateContentType(HttpRequest request, params string[] allowedContentTypes)
    {
        var bodyDetection = request.HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>();
        if (bodyDetection?.CanHaveBody != true)
            return null;

        string? contentType = request.ContentType?.Split(';', 2)[0].Trim();
        return allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase)
            ? null
            : global::Microsoft.AspNetCore.Http.Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
    }
}
