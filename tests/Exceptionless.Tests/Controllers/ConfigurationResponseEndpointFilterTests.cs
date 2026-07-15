using Exceptionless.Core.Models;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Utility;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class ConfigurationResponseEndpointFilterTests
{
    [Theory]
    [InlineData(StatusCodes.Status200OK)]
    [InlineData(StatusCodes.Status202Accepted)]
    public async Task InvokeAsync_SuccessResult_AddsConfigurationVersionHeader(int statusCode)
    {
        var httpContext = CreateContext("/api/v2/events", configurationVersion: 42);

        await InvokeAsync(httpContext, Results.StatusCode(statusCode));

        Assert.Equal("42", httpContext.Response.Headers[Headers.ConfigurationVersion]);
        Assert.False(httpContext.Response.Headers.ContainsKey(Headers.LegacyConfigurationVersion));
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status413RequestEntityTooLarge)]
    [InlineData(StatusCodes.Status500InternalServerError)]
    public async Task InvokeAsync_ErrorResult_DoesNotAddConfigurationVersionHeader(int statusCode)
    {
        var httpContext = CreateContext("/api/v2/events", configurationVersion: 42);

        await InvokeAsync(httpContext, Results.StatusCode(statusCode));

        AssertNoConfigurationVersionHeaders(httpContext);
    }

    [Fact]
    public async Task InvokeAsync_ResultWithoutExplicitStatusCode_DoesNotAddConfigurationVersionHeader()
    {
        var httpContext = CreateContext("/api/v2/events", configurationVersion: 42);

        await InvokeAsync(httpContext, new ResultWithoutStatusCode());

        AssertNoConfigurationVersionHeaders(httpContext);
    }

    [Fact]
    public async Task InvokeAsync_MissingProject_DoesNotAddConfigurationVersionHeader()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v2/events";

        await InvokeAsync(httpContext, Results.Ok());

        AssertNoConfigurationVersionHeaders(httpContext);
    }

    [Theory]
    [InlineData("/api/v1/events", Headers.LegacyConfigurationVersion, Headers.ConfigurationVersion)]
    [InlineData("/api/v2/events", Headers.ConfigurationVersion, Headers.LegacyConfigurationVersion)]
    public async Task InvokeAsync_ApiVersion_AddsExpectedHeader(string path, string expectedHeader, string unexpectedHeader)
    {
        var httpContext = CreateContext(path, configurationVersion: 42);

        await InvokeAsync(httpContext, Results.Accepted());

        Assert.Equal("42", httpContext.Response.Headers[expectedHeader]);
        Assert.False(httpContext.Response.Headers.ContainsKey(unexpectedHeader));
    }

    private static DefaultHttpContext CreateContext(string path, int configurationVersion)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Request.SetProject(new Project
        {
            Id = "project-id",
            OrganizationId = "organization-id",
            Name = "Test Project",
            Configuration = new ClientConfiguration { Version = configurationVersion }
        });
        return httpContext;
    }

    private static async Task<object?> InvokeAsync(HttpContext httpContext, object result)
    {
        var filter = new ConfigurationResponseEndpointFilter();
        var context = new TestEndpointFilterInvocationContext(httpContext);
        return await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(result));
    }

    private static void AssertNoConfigurationVersionHeaders(HttpContext httpContext)
    {
        Assert.False(httpContext.Response.Headers.ContainsKey(Headers.ConfigurationVersion));
        Assert.False(httpContext.Response.Headers.ContainsKey(Headers.LegacyConfigurationVersion));
    }

    private sealed class ResultWithoutStatusCode : Microsoft.AspNetCore.Http.IResult
    {
        public Task ExecuteAsync(HttpContext httpContext) => Task.CompletedTask;
    }

    private sealed class TestEndpointFilterInvocationContext(HttpContext httpContext) : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext { get; } = httpContext;
        public override IList<object?> Arguments { get; } = [];
        public override T GetArgument<T>(int index) => throw new ArgumentOutOfRangeException(nameof(index));
    }
}
