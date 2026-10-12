using System.Security.Claims;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Services;
using Exceptionless.Core.Validation;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class OAuthAuthorizationValidationTests(ITestOutputHelper output) : TestWithServices(output)
{
    private const string Resource = "http://localhost/mcp";
    private const string RedirectUri = "http://localhost/callback";
    private const string FourReadScopes = "mcp:read projects:read stacks:read events:read";
    private const string AllScopes = "mcp:read projects:read stacks:read stacks:write events:read offline_access";

    protected override void RegisterServices(IServiceCollection services, AppOptions options)
    {
        base.RegisterServices(services, options);
        services.AddSingleton(new OAuthServerOptions());
    }

    [Fact]
    public void GetActiveOAuthOrganizationIds_RemovedMembership_ReturnsNoOrganizations()
    {
        // Arrange
        var user = new User { OrganizationIds = new HashSet<string>([TestConstants.OrganizationId]) };
        var token = new OAuthToken { OrganizationIds = [TestConstants.OrganizationId] };
        user.OrganizationIds.Clear();

        // Act
        var organizationIds = user.GetActiveOAuthOrganizationIds(token);

        // Assert
        Assert.Empty(organizationIds);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ToIdentity_OAuthGrant_DoesNotInheritGlobalRoleOrForeignOrganization(bool globalAdministrator, bool writeScope)
    {
        // Arrange
        var user = new User
        {
            Id = TestConstants.UserId,
            EmailAddress = "member@example.test",
            Roles = new HashSet<string>(globalAdministrator ? [AuthorizationRoles.User, AuthorizationRoles.GlobalAdmin] : [AuthorizationRoles.User]),
            OrganizationIds = new HashSet<string>([TestConstants.OrganizationId])
        };
        var token = new OAuthToken
        {
            Id = TestConstants.TokenId,
            ClientId = "client-1",
            Resource = Resource,
            Scopes = writeScope ? [AuthorizationRoles.McpRead, AuthorizationRoles.StacksWrite, AuthorizationRoles.OfflineAccess] : [AuthorizationRoles.McpRead],
            OrganizationIds = [TestConstants.OrganizationId, TestConstants.OrganizationId2]
        };

        // Act
        var identity = user.ToIdentity(token);

        // Assert
        Assert.DoesNotContain(identity.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == AuthorizationRoles.GlobalAdmin);
        Assert.Equal(writeScope, identity.HasClaim(ClaimTypes.Role, AuthorizationRoles.StacksWrite));
        Assert.Equal(TestConstants.OrganizationId, identity.FindFirst(IdentityUtils.OrganizationIdsClaim)?.Value);
    }

    [Theory]
    [InlineData(null, AllScopes, "stacks:write")]
    [InlineData(FourReadScopes, AllScopes, "stacks:write, offline_access")]
    [InlineData(AllScopes, AllScopes, null)]
    [InlineData(FourReadScopes, FourReadScopes, null)]
    [InlineData(FourReadScopes, "mcp:read", null)]
    public async Task ValidateAuthorizationRequestAsync_ClientScopeCombinations_EnforcesAllowedScopes(string? clientScopes, string requestScopes, string? deniedScopes)
    {
        // Arrange
        using var repository = CreateRepository(clientScopes);
        var service = CreateService(repository);
        var application = repository.Application;
        string[] originalScopes = application.Scopes.ToArray();

        // Act
        var result = await service.ValidateAuthorizationRequestAsync(CreateRequest(requestScopes), Resource, OAuthService.McpResource);

        // Assert
        Assert.Equal(deniedScopes is null, result.IsValid);
        Assert.Equal(originalScopes, application.Scopes);
        if (deniedScopes is not null)
        {
            Assert.Equal("invalid_scope", result.Error);
            Assert.Equal($"Scopes not allowed for this application: {deniedScopes}. Restart authorization with scopes allowed for this application.", result.ErrorDescription);
            Assert.Empty(result.Scopes);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("offline_access")]
    [InlineData("projects:read")]
    [InlineData("mcp:read <script>unknown</script>")]
    public async Task ValidateAuthorizationRequestAsync_InvalidScopes_DeniesWithoutReflectingUnknownScopes(string scopes)
    {
        // Arrange
        using var repository = CreateRepository(AllScopes);
        var service = CreateService(repository);

        // Act
        var result = await service.ValidateAuthorizationRequestAsync(CreateRequest(scopes), Resource, OAuthService.McpResource);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("invalid_scope", result.Error);
        Assert.DoesNotContain("<script>", result.ErrorDescription);
        Assert.Empty(result.Scopes);
    }

    [Theory]
    [InlineData("unknown", "invalid_client", "Unknown OAuth client.")]
    [InlineData("disabled", "invalid_client", "Unknown OAuth client.")]
    [InlineData("redirect", "invalid_request", "Invalid redirect_uri.")]
    [InlineData("pkce", "invalid_request", "PKCE S256 is required.")]
    [InlineData("resource", "invalid_target", "The requested resource is not supported.")]
    public async Task ValidateAuthorizationRequestAsync_InvalidSecurityParameters_DoesNotDiscloseClientScopeDetails(string scenario, string error, string description)
    {
        // Arrange
        using var repository = CreateRepository(FourReadScopes);
        var service = CreateService(repository);
        var application = repository.Application;
        application.IsDisabled = scenario == "disabled";
        var request = CreateRequest(AllScopes) with
        {
            ClientId = scenario == "unknown" ? "unknown" : application.ClientId,
            RedirectUri = scenario == "redirect" ? "https://attacker.example/callback" : RedirectUri,
            CodeChallenge = scenario == "pkce" ? "invalid" : new string('a', 43),
            Resource = scenario == "resource" ? "http://localhost/other" : Resource
        };

        // Act
        var result = await service.ValidateAuthorizationRequestAsync(request, Resource, OAuthService.McpResource);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Equal(description, result.ErrorDescription);
        Assert.Empty(result.Scopes);
    }

    private static OAuthAuthorizeRequest CreateRequest(string scopes) => new()
    {
        ClientId = "test-client",
        ResponseType = "code",
        RedirectUri = RedirectUri,
        CodeChallenge = new string('a', 43),
        CodeChallengeMethod = OAuthService.CodeChallengeMethod,
        Resource = Resource,
        Scope = scopes
    };

    private TestOAuthApplicationRepository CreateRepository(string? scopes)
    {
        var application = new OAuthApplication
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ClientId = "test-client",
            Name = "Test Client",
            RedirectUris = [RedirectUri],
            Scopes = scopes?.Split(' ') ?? OAuthService.DefaultScopes.ToArray()
        };
        return new TestOAuthApplicationRepository(
            GetService<ExceptionlessElasticConfiguration>(), GetService<MiniValidationValidator>(), GetService<AppOptions>(), application);
    }

    private OAuthService CreateService(IOAuthApplicationRepository repository) => new(
        GetService<OAuthServerOptions>(),
        GetService<ICacheClient>(),
        GetService<ILockProvider>(),
        repository,
        GetService<IOAuthClientMetadataService>(),
        GetService<IOAuthTokenRepository>(),
        GetService<IUserRepository>(),
        TimeProvider);

    private sealed class TestOAuthApplicationRepository(
        ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options, OAuthApplication application)
        : OAuthApplicationRepository(configuration, validator, options), IOAuthApplicationRepository
    {
        public OAuthApplication Application { get; } = application;

        Task<OAuthApplication?> IOAuthApplicationRepository.GetByClientIdAsync(string clientId, CommandOptionsDescriptor<OAuthApplication>? options)
            => Task.FromResult(clientId == Application.ClientId ? Application : null);
    }
}
