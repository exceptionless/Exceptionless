using System.Security.Claims;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Utility;
using Xunit;

namespace Exceptionless.Tests.Utility.Handlers;

public sealed class OverageMiddlewareTests : IntegrationTestsBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly UsageService _usageService;

    public OverageMiddlewareTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _organizationRepository = GetService<IOrganizationRepository>();
        _userRepository = GetService<IUserRepository>();
        _usageService = GetService<UsageService>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task Invoke_SuspendedOrganizationUserRequest_ReturnsPaymentRequired()
    {
        // Arrange
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);

        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        organization.IsSuspended = true;
        organization.SuspensionCode = SuspensionCode.Billing;
        organization.SuspensionDate = DateTime.UtcNow;
        organization.SuspendedByUserId = user.Id;
        await _organizationRepository.SaveAsync(organization);

        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;

            return Task.CompletedTask;
        });
        var context = CreateEventPostContext(new ClaimsPrincipal(user.ToIdentity()), 128);

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status402PaymentRequired, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_MissingContentLength_CallsNext()
    {
        // Arrange
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;

            return Task.CompletedTask;
        });
        var context = CreateEventPostContext(CreateTokenPrincipal(SampleDataService.TEST_API_KEY, SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID));

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_ZeroContentLength_ReturnsLengthRequired()
    {
        // Arrange
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;

            return Task.CompletedTask;
        });
        var context = CreateEventPostContext(
            CreateTokenPrincipal(SampleDataService.TEST_API_KEY, SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID),
            0);

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status411LengthRequired, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_PayloadTooLarge_ReturnsRequestEntityTooLarge()
    {
        // Arrange
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;

            return Task.CompletedTask;
        });
        var context = CreateEventPostContext(
            CreateTokenPrincipal(SampleDataService.TEST_API_KEY, SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID),
            GetService<AppOptions>().MaximumEventPostSize + 1);

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_OrganizationOverUsageLimit_ReturnsPaymentRequired()
    {
        // Arrange
        await _usageService.IncrementTotalAsync(SampleDataService.FREE_ORG_ID, SampleDataService.FREE_PROJECT_ID, 1_000_000);

        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;

            return Task.CompletedTask;
        });
        var context = CreateEventPostContext(
            CreateTokenPrincipal(SampleDataService.FREE_API_KEY, SampleDataService.FREE_ORG_ID, SampleDataService.FREE_PROJECT_ID),
            128);

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status402PaymentRequired, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_NonEventPostRequest_CallsNext()
    {
        // Arrange
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;

            return Task.CompletedTask;
        });
        var context = CreateContext(HttpMethods.Get, "/api/v2/projects");

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_EventSubmissionDisabled_ReturnsServiceUnavailable()
    {
        // Arrange
        var appOptions = GetService<AppOptions>();
        appOptions.EventSubmissionDisabled = true;

        try
        {
            bool nextCalled = false;
            var middleware = CreateMiddleware(context =>
            {
                nextCalled = true;

                return Task.CompletedTask;
            });
            var context = CreateEventPostContext(
                CreateTokenPrincipal(SampleDataService.TEST_API_KEY, SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID),
                128);

            // Act
            await middleware.Invoke(context);

            // Assert
            Assert.False(nextCalled);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        }
        finally
        {
            appOptions.EventSubmissionDisabled = false;
        }
    }

    private OverageMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new OverageMiddleware(
            next,
            _usageService,
            _organizationRepository,
            GetService<AppOptions>(),
            GetService<ILogger<OverageMiddleware>>());
    }

    private static ClaimsPrincipal CreateTokenPrincipal(string tokenId, string organizationId, string projectId)
    {
        var token = new Token
        {
            Id = tokenId,
            Type = TokenType.Access,
            OrganizationId = organizationId,
            ProjectId = projectId
        };

        return new ClaimsPrincipal(token.ToIdentity());
    }

    private static DefaultHttpContext CreateEventPostContext(ClaimsPrincipal user, long? contentLength = null)
    {
        var context = CreateContext(HttpMethods.Post, "/api/v2/events");
        context.User = user;
        if (contentLength.HasValue)
            context.Request.Headers.ContentLength = contentLength.Value;

        return context;
    }

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }
}
