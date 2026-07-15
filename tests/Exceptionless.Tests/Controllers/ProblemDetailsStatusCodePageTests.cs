using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class ProblemDetailsStatusCodePageTests
{
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, null)]
    [InlineData(StatusCodes.Status401Unauthorized, null)]
    [InlineData(StatusCodes.Status403Forbidden, null)]
    [InlineData(StatusCodes.Status404NotFound, null)]
    [InlineData(StatusCodes.Status404NotFound, "text/plain")]
    [InlineData(StatusCodes.Status409Conflict, null)]
    [InlineData(StatusCodes.Status413RequestEntityTooLarge, null)]
    [InlineData(StatusCodes.Status415UnsupportedMediaType, null)]
    [InlineData(StatusCodes.Status422UnprocessableEntity, null)]
    [InlineData(StatusCodes.Status426UpgradeRequired, null)]
    [InlineData(StatusCodes.Status429TooManyRequests, null)]
    [InlineData(StatusCodes.Status500InternalServerError, null)]
    [InlineData(StatusCodes.Status501NotImplemented, null)]
    [InlineData(StatusCodes.Status503ServiceUnavailable, null)]
    public async Task WriteProblemDetailsStatusCodeResponseAsync_WithErrorStatus_PreservesStatusAndWritesProblemDetails(int statusCode, string? acceptHeader)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        await using var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };
        httpContext.Response.Body = new MemoryStream();
        httpContext.Response.StatusCode = statusCode;
        if (acceptHeader is not null)
            httpContext.Request.Headers.Accept = acceptHeader;

        var statusCodeContext = new StatusCodeContext(
            httpContext,
            new StatusCodePagesOptions(),
            _ => Task.CompletedTask);

        await Exceptionless.Web.Program.WriteProblemDetailsStatusCodeResponseAsync(statusCodeContext);

        Assert.Equal(statusCode, httpContext.Response.StatusCode);
        Assert.Equal("application/problem+json", httpContext.Response.ContentType);

        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(httpContext.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(statusCode, document.RootElement.GetProperty("status").GetInt32());
    }
}
