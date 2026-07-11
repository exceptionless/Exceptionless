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
        var bodyDetection = request.HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>();
        if (bodyDetection?.CanHaveBody != true)
            return null;

        string? contentType = request.ContentType?.Split(';', 2)[0].Trim();
        return String.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase)
            ? null
            : global::Microsoft.AspNetCore.Http.Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
    }
}
