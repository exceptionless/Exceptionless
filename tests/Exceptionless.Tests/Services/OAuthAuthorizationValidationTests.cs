using System.Reflection;
using System.Security.Claims;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class OAuthAuthorizationValidationTests
{
    private const string Resource = "http://localhost/mcp";
    private const string RedirectUri = "http://localhost/callback";
    private const string FourReadScopes = "mcp:read projects:read stacks:read events:read";
    private const string AllScopes = "mcp:read projects:read stacks:read stacks:write events:read offline_access";

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ToIdentity_OAuthGrant_DoesNotInheritGlobalRoleOrForeignOrganization(bool globalAdministrator, bool writeScope)
    {
        var user = new User
        {
            Id = "user-1",
            EmailAddress = "member@example.test",
            Roles = new HashSet<string>(globalAdministrator ? [AuthorizationRoles.User, AuthorizationRoles.GlobalAdmin] : [AuthorizationRoles.User]),
            OrganizationIds = new HashSet<string>(["organization-1"])
        };
        var token = new OAuthToken
        {
            Id = "token-1",
            ClientId = "client-1",
            Resource = Resource,
            Scopes = writeScope ? [AuthorizationRoles.McpRead, AuthorizationRoles.StacksWrite, AuthorizationRoles.OfflineAccess] : [AuthorizationRoles.McpRead],
            OrganizationIds = ["organization-1", "foreign-organization"]
        };

        var identity = user.ToIdentity(token);

        Assert.DoesNotContain(identity.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == AuthorizationRoles.GlobalAdmin);
        Assert.Equal(writeScope, identity.HasClaim(ClaimTypes.Role, AuthorizationRoles.StacksWrite));
        Assert.Equal("organization-1", identity.FindFirst(IdentityUtils.OrganizationIdsClaim)?.Value);
        user.OrganizationIds.Clear();
        Assert.Empty(user.GetActiveOAuthOrganizationIds(token));
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
        var (service, application) = CreateService(clientScopes);
        string[] originalScopes = application.Scopes.ToArray();

        // Act
        var result = await service.ValidateAuthorizationRequestAsync(CreateRequest(requestScopes), Resource, OAuthService.McpResource);

        // Assert
        Assert.Equal(deniedScopes is null, result.IsValid);
        Assert.Equal(originalScopes, application.Scopes);
        if (deniedScopes is not null)
        {
            Assert.Equal("invalid_scope", result.Error);
            Assert.Equal($"Scopes not allowed for this application: {deniedScopes}. Restart authorization with fewer scopes or ask a global administrator to review the application in System → OAuth Apps. After saving changes, restart authorization using the same client ID.", result.ErrorDescription);
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
        var (service, _) = CreateService(AllScopes);

        var result = await service.ValidateAuthorizationRequestAsync(CreateRequest(scopes), Resource, OAuthService.McpResource);

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
        var (service, application) = CreateService(FourReadScopes);
        application.IsDisabled = scenario == "disabled";
        var request = CreateRequest(AllScopes) with
        {
            ClientId = scenario == "unknown" ? "unknown" : application.ClientId,
            RedirectUri = scenario == "redirect" ? "https://attacker.example/callback" : RedirectUri,
            CodeChallenge = scenario == "pkce" ? "invalid" : new string('a', 43),
            Resource = scenario == "resource" ? "http://localhost/other" : Resource
        };

        var result = await service.ValidateAuthorizationRequestAsync(request, Resource, OAuthService.McpResource);

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

    private static (OAuthService Service, OAuthApplication Application) CreateService(string? scopes)
    {
        var application = new OAuthApplication
        {
            ClientId = "test-client",
            Name = "Test Client",
            RedirectUris = [RedirectUri],
            Scopes = scopes?.Split(' ') ?? OAuthService.DefaultScopes.ToArray()
        };
        var repository = DispatchProxy.Create<IOAuthApplicationRepository, ApplicationRepositoryProxy>();
        ((ApplicationRepositoryProxy)(object)repository).Application = application;
        var service = new OAuthService(new OAuthServerOptions(), null!, null!, repository, null!, null!, null!, TimeProvider.System);
        return (service, application);
    }

    private class ApplicationRepositoryProxy : DispatchProxy
    {
        public OAuthApplication Application { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IOAuthApplicationRepository.GetByClientIdAsync))
                return Task.FromResult(args?[0] as string == Application.ClientId ? Application : null);

            throw new NotSupportedException($"Unexpected repository call: {targetMethod?.Name}");
        }
    }
}
