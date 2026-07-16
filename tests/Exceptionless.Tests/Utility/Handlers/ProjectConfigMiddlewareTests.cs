using System.Security.Claims;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Utility;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Utility.Handlers;

public sealed class ProjectConfigMiddlewareTests : IntegrationTestsBase
{
    public ProjectConfigMiddlewareTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task Invoke_ConfigRouteWithPost_CallsNext()
    {
        // Arrange
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext("/api/v2/projects/config");
        context.Request.Method = HttpMethods.Post;

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_ConfigRouteWithoutProject_ReturnsUnauthorized()
    {
        // Arrange
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext("/api/v2/projects/config");

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_NonConfigRoute_CallsNext()
    {
        // Arrange
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext("/api/v2/projects");

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_UnknownProject_ReturnsNotFound()
    {
        // Arrange
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAuthenticatedContext("/api/v2/projects/config", TestConstants.InvalidProjectId);

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_V1ConfigRouteWithProject_ReturnsConfigurationJson()
    {
        // Arrange
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAuthenticatedContext("/api/v1/project/config", SampleDataService.TEST_PROJECT_ID);

        // Act
        await middleware.Invoke(context);

        // Assert
        var configuration = ReadConfiguration(context);
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
        Assert.Equal(0, configuration.Version);
        Assert.Equal("true", configuration.Settings["IncludeConditionalData"]);
    }

    [Fact]
    public async Task Invoke_V2ConfigRouteWithCurrentVersion_ReturnsNotModified()
    {
        // Arrange
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAuthenticatedContext("/api/v2/projects/config?v=0", SampleDataService.TEST_PROJECT_ID);

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
        Assert.Equal(String.Empty, ReadResponseBody(context));
    }

    [Fact]
    public async Task Invoke_V2ConfigRouteWithProject_ReturnsConfigurationJson()
    {
        // Arrange
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAuthenticatedContext("/api/v2/projects/config", SampleDataService.TEST_PROJECT_ID);

        // Act
        await middleware.Invoke(context);

        // Assert
        var configuration = ReadConfiguration(context);
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
        Assert.Equal(0, configuration.Version);
        Assert.Equal("true", configuration.Settings["IncludeConditionalData"]);
    }

    private ProjectConfigMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new ProjectConfigMiddleware(
            next,
            GetService<IProjectRepository>(),
            GetService<ITextSerializer>());
    }

    private static DefaultHttpContext CreateAuthenticatedContext(string pathAndQuery, string projectId)
    {
        var context = CreateContext(pathAndQuery);
        var token = new Token
        {
            Id = SampleDataService.TEST_API_KEY,
            Type = TokenType.Access,
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = projectId
        };

        context.User = new ClaimsPrincipal(token.ToIdentity());
        return context;
    }

    private static DefaultHttpContext CreateContext(string pathAndQuery)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        string[] parts = pathAndQuery.Split('?', 2);
        context.Request.Path = parts[0];
        if (parts.Length == 2)
            context.Request.QueryString = new QueryString("?" + parts[1]);

        context.Response.Body = new MemoryStream();
        return context;
    }

    private ClientConfiguration ReadConfiguration(DefaultHttpContext context)
    {
        string json = ReadResponseBody(context);
        var configuration = GetService<ITextSerializer>().Deserialize<ClientConfiguration>(json);
        Assert.NotNull(configuration);
        return configuration;
    }

    private static string ReadResponseBody(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return reader.ReadToEnd();
    }
}
