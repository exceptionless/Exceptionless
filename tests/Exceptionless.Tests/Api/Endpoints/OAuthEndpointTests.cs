using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Models.Admin;
using Exceptionless.Web.Models.OAuth;
using FluentRest;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class OAuthEndpointTests : IntegrationTestsBase
{
    private const string ClientId = "test-oauth-client";
    private const string RedirectUri = "http://localhost/callback";
    private const string MetadataClientId = "https://oauth.example/client.json";
    private const string MetadataNoScopeClientId = "https://oauth.example/no-scope-client.json";
    private const string MetadataMissingNameClientId = "https://oauth.example/missing-name-client.json";
    private const string MetadataRedirectUri = "https://oauth.example/callback";
    private const string ClaudeMetadataClientId = "https://claude.ai/oauth/claude-code-client-metadata";
    private const string ClaudeLoopbackRedirectUri = "http://localhost:48272/callback";
    private const string Resource = "http://localhost:7110/mcp";
    private const string RestApiResource = "http://localhost:7110/api/v2";
    private const string PkceVerifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
    private const string WrongPkceVerifier = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

    private readonly IOAuthApplicationRepository _oauthApplicationRepository;
    private readonly IOAuthTokenRepository _oauthTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly AppWebHostFactory _factory;

    public OAuthEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _factory = factory;
        _oauthApplicationRepository = GetService<IOAuthApplicationRepository>();
        _oauthTokenRepository = GetService<IOAuthTokenRepository>();
        _userRepository = GetService<IUserRepository>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);
        services.ReplaceSingleton<IOAuthClientMetadataService, FakeOAuthClientMetadataService>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
        await CreateStoredOAuthApplicationAsync(ClientId, RedirectUri);
    }

    [Fact]
    public async Task GetAuthorizationServerMetadataAsync_ReturnsOAuthMetadata()
    {
        using var client = _server.CreateClient();

        var response = await client.GetAsync("/.well-known/oauth-authorization-server", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var metadata = await DeserializeResponseAsync<OAuthAuthorizationServerMetadata>(response);
        Assert.NotNull(metadata);
        Assert.Equal("http://localhost:7110", metadata.Issuer);
        Assert.Equal("http://localhost:7110/api/v2/oauth/authorize", metadata.AuthorizationEndpoint);
        Assert.Equal("http://localhost:7110/api/v2/oauth/token", metadata.TokenEndpoint);
        Assert.Equal("http://localhost:7110/api/v2/oauth/register", metadata.RegistrationEndpoint);
        Assert.Contains(OAuthGrantTypes.AuthorizationCode, metadata.GrantTypesSupported);
        Assert.Contains(OAuthService.CodeChallengeMethod, metadata.CodeChallengeMethodsSupported);
        Assert.Contains(AuthorizationRoles.McpRead, metadata.ScopesSupported);
        Assert.Contains(AuthorizationRoles.StacksWrite, metadata.ScopesSupported);
        Assert.True(metadata.ClientIdMetadataDocumentSupported);
        Assert.True(metadata.AuthorizationResponseIssParameterSupported);
    }

    [Theory]
    [InlineData("/mcp", "/mcp", AuthorizationRoles.McpRead, HttpStatusCode.MethodNotAllowed)]
    [InlineData("/api/v2", "/api/v2/projects", AuthorizationRoles.ProjectsRead, HttpStatusCode.OK)]
    public async Task OAuthAsync_SeparateOrigins_UsesApiForDiscoveryTokensAndChallenges(string resourcePath, string protectedPath, string scope, HttpStatusCode authorizedStatus)
    {
        // Arrange
        const string apiOrigin = "https://api.localhost:7443";
        const string applicationOrigin = "https://ui.localhost:7444";
        string resource = apiOrigin + resourcePath;
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BaseURL"] = applicationOrigin + "/#!",
                ["ApiUrl"] = apiOrigin + "/"
            })));
        await factory.Server.WaitForReadyAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(apiOrigin + "/api/v2/"),
            AllowAutoRedirect = false
        });

        using var discoveryRequest = new HttpRequestMessage(HttpMethod.Get, "/.well-known/oauth-authorization-server");
        discoveryRequest.Headers.Host = "untrusted.localhost";
        // Act
        var discoveryResponse = await client.SendAsync(discoveryRequest, TestContext.Current.CancellationToken);
        var metadata = await DeserializeResponseAsync<OAuthAuthorizationServerMetadata>(discoveryResponse);
        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(apiOrigin, metadata.Issuer);
        Assert.Equal(apiOrigin + "/api/v2/oauth/authorize", metadata.AuthorizationEndpoint);
        Assert.Equal(apiOrigin + "/api/v2/oauth/token", metadata.TokenEndpoint);
        Assert.Equal(apiOrigin + "/api/v2/oauth/register", metadata.RegistrationEndpoint);
        Assert.Equal(apiOrigin + "/api/v2/oauth/revoke", metadata.RevocationEndpoint);

        // Act
        var resourceResponse = await client.GetAsync("/.well-known/oauth-protected-resource" + resourcePath, TestContext.Current.CancellationToken);
        var resourceMetadata = await DeserializeResponseAsync<OAuthProtectedResourceMetadata>(resourceResponse);
        // Assert
        Assert.NotNull(resourceMetadata);
        Assert.Equal(resource, resourceMetadata.Resource);
        Assert.Equal([apiOrigin], resourceMetadata.AuthorizationServers);

        // Arrange
        using var bridgeRequest = CreateAuthorizeRequest(PkceVerifier, resource: resource, authenticate: false);
        string query = new Uri(client.BaseAddress!, bridgeRequest.RequestUri!).Query;
        // Act
        var bridgeResponse = await client.SendAsync(bridgeRequest, TestContext.Current.CancellationToken);
        // Assert
        Assert.Equal(HttpStatusCode.Redirect, bridgeResponse.StatusCode);
        Assert.Equal(applicationOrigin + "/oauth/authorize" + query, bridgeResponse.Headers.Location?.OriginalString);

        // Arrange
        using var consentRequest = CreateAuthorizeJsonRequest(PkceVerifier, resource: resource, scope: scope);
        consentRequest.RequestUri = new Uri("oauth/authorize/consent", UriKind.Relative);
        // Act
        var consentResponse = await client.SendAsync(consentRequest, TestContext.Current.CancellationToken);
        // Assert
        Assert.Equal(HttpStatusCode.OK, consentResponse.StatusCode);
        var consent = await DeserializeResponseAsync<OAuthAuthorizeConsentResponse>(consentResponse);
        Assert.NotNull(consent);
        Assert.Equal(resource, consent.Resource);

        // Arrange
        using var authorizationRequest = CreateAuthorizeJsonRequest(PkceVerifier, resource: resource, scope: scope);
        // Act
        var authorizationResponse = await client.SendAsync(authorizationRequest, TestContext.Current.CancellationToken);
        // Assert
        Assert.Equal(HttpStatusCode.OK, authorizationResponse.StatusCode);
        var authorization = await DeserializeResponseAsync<OAuthAuthorizeResponse>(authorizationResponse);
        Assert.NotNull(authorization);
        var authorizationQuery = QueryHelpers.ParseQuery(new Uri(authorization.RedirectUri).Query);
        Assert.Equal(apiOrigin, authorizationQuery["iss"].ToString());

        // Arrange
        using var tokenContent = CreateTokenExchangeContent(authorizationQuery["code"].ToString(), PkceVerifier, resource: resource);
        // Act
        var tokenResponse = await client.PostAsync("oauth/token", tokenContent, TestContext.Current.CancellationToken);
        // Assert
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var token = await DeserializeResponseAsync<OAuthTokenResponse>(tokenResponse);
        Assert.NotNull(token);
        Assert.Equal(resource, token.Resource);

        // Act
        var challengeResponse = await client.GetAsync(protectedPath, TestContext.Current.CancellationToken);
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, challengeResponse.StatusCode);
        Assert.Contains($"resource_metadata=\"{apiOrigin}/.well-known/oauth-protected-resource{resourcePath}\"", challengeResponse.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);

        // Arrange
        using var protectedRequest = new HttpRequestMessage(HttpMethod.Get, protectedPath);
        protectedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        protectedRequest.Headers.Add("Origin", apiOrigin);
        // Act
        var protectedResponse = await client.SendAsync(protectedRequest, TestContext.Current.CancellationToken);
        // Assert
        Assert.Equal(authorizedStatus, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task RegisterAsync_ValidDynamicClient_ReturnsClientAndPersistsApplication()
    {
        using var client = CreateHttpClient();

        var response = await client.PostAsJsonAsync("oauth/register", new OAuthClientRegistrationRequest
        {
            ClientName = "Codex",
            RedirectUris = ["http://127.0.0.1:49152/callback"],
            GrantTypes = [OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken],
            ResponseTypes = ["code"],
            Scope = $"{AuthorizationRoles.McpRead} {AuthorizationRoles.ProjectsRead}",
            TokenEndpointAuthMethod = "none"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registration = await DeserializeResponseAsync<OAuthClientRegistrationResponse>(response);
        Assert.NotNull(registration);
        Assert.StartsWith("dcr_", registration.ClientId, StringComparison.Ordinal);
        Assert.Equal("Codex", registration.ClientName);
        Assert.Contains("http://127.0.0.1:49152/callback", registration.RedirectUris);
        Assert.Contains(OAuthGrantTypes.AuthorizationCode, registration.GrantTypes);
        Assert.Equal("none", registration.TokenEndpointAuthMethod);
        Assert.Contains(AuthorizationRoles.McpRead, registration.Scope);

        var application = await _oauthApplicationRepository.GetByClientIdAsync(registration.ClientId, o => o.ImmediateConsistency());
        Assert.NotNull(application);
        Assert.Equal(OAuthApplication.SystemUserId, application.CreatedByUserId);
        Assert.Contains("http://127.0.0.1:49152/callback", application.RedirectUris);
        Assert.Contains(AuthorizationRoles.ProjectsRead, application.Scopes);
    }

    [Fact]
    public async Task RegisterAsync_WithoutScope_DefaultsToAllScopes()
    {
        using var client = CreateHttpClient();

        var response = await client.PostAsJsonAsync("oauth/register", new OAuthClientRegistrationRequest
        {
            ClientName = "Read Only Client",
            RedirectUris = ["http://127.0.0.1:49152/callback"],
            GrantTypes = [OAuthGrantTypes.AuthorizationCode],
            ResponseTypes = ["code"],
            TokenEndpointAuthMethod = "none"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registration = await DeserializeResponseAsync<OAuthClientRegistrationResponse>(response);
        Assert.NotNull(registration);
        Assert.Contains(AuthorizationRoles.McpRead, registration.Scope);
        Assert.Contains(AuthorizationRoles.ProjectsRead, registration.Scope);
        Assert.Contains(AuthorizationRoles.SavedViewsRead, registration.Scope);
        Assert.Contains(AuthorizationRoles.SavedViewsWrite, registration.Scope);
        Assert.Contains(AuthorizationRoles.StacksRead, registration.Scope);
        Assert.Contains(AuthorizationRoles.EventsRead, registration.Scope);
        Assert.Contains(AuthorizationRoles.StacksWrite, registration.Scope);
        Assert.Contains(AuthorizationRoles.OfflineAccess, registration.Scope);

        var application = await _oauthApplicationRepository.GetByClientIdAsync(registration.ClientId, o => o.ImmediateConsistency());
        Assert.NotNull(application);
        Assert.Contains(AuthorizationRoles.McpRead, application.Scopes);
        Assert.Contains(AuthorizationRoles.ProjectsRead, application.Scopes);
        Assert.Contains(AuthorizationRoles.StacksRead, application.Scopes);
        Assert.Contains(AuthorizationRoles.EventsRead, application.Scopes);
        Assert.Contains(AuthorizationRoles.StacksWrite, application.Scopes);
        Assert.Contains(AuthorizationRoles.OfflineAccess, application.Scopes);
    }

    [Fact]
    public async Task GetAuthorizeConsentAsync_DynamicClientAllowsEquivalentLoopbackHostWithDifferentPort_ReturnsClientDetails()
    {
        using var client = CreateHttpClient();

        var registrationResponse = await client.PostAsJsonAsync("oauth/register", new OAuthClientRegistrationRequest
        {
            ClientName = "Copilot",
            RedirectUris = ["http://localhost"],
            GrantTypes = [OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken],
            ResponseTypes = ["code"],
            TokenEndpointAuthMethod = "none"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
        var registration = await DeserializeResponseAsync<OAuthClientRegistrationResponse>(registrationResponse);
        Assert.NotNull(registration);

        using var request = CreateAuthorizeJsonRequest(
            PkceVerifier,
            "http://127.0.0.1:63952/",
            clientId: registration.ClientId,
            organizationIds: []);
        request.RequestUri = new Uri("oauth/authorize/consent", UriKind.Relative);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var consent = await DeserializeResponseAsync<OAuthAuthorizeConsentResponse>(response);
        Assert.NotNull(consent);
        Assert.Equal(registration.ClientId, consent.ClientId);
        Assert.Equal("Copilot", consent.ClientName);
        Assert.Equal("http://127.0.0.1:63952/", consent.RedirectUri);
    }

    [Fact]
    public async Task RegisterAsync_TooManyAttempts_ReturnsTooManyRequests()
    {
        using var client = CreateHttpClient();

        for (int i = 0; i < 20; i++)
        {
            var allowedResponse = await client.PostAsJsonAsync("oauth/register", new OAuthClientRegistrationRequest
            {
                ClientName = $"Client {i}",
                RedirectUris = ["http://127.0.0.1:49152/callback"],
                GrantTypes = [OAuthGrantTypes.AuthorizationCode],
                ResponseTypes = ["code"],
                Scope = AuthorizationRoles.McpRead,
                TokenEndpointAuthMethod = "none"
            }, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Created, allowedResponse.StatusCode);
        }

        var limitedResponse = await client.PostAsJsonAsync("oauth/register", new OAuthClientRegistrationRequest
        {
            ClientName = "Limited Client",
            RedirectUris = ["http://127.0.0.1:49152/callback"],
            GrantTypes = [OAuthGrantTypes.AuthorizationCode],
            ResponseTypes = ["code"],
            Scope = AuthorizationRoles.McpRead,
            TokenEndpointAuthMethod = "none"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        var error = await limitedResponse.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("temporarily_unavailable", error.Error);
    }

    [Fact]
    public async Task RegisterAsync_InvalidRedirectUri_ReturnsBadRequest()
    {
        using var client = CreateHttpClient();

        var response = await client.PostAsJsonAsync("oauth/register", new OAuthClientRegistrationRequest
        {
            ClientName = "Bad Client",
            RedirectUris = ["http://attacker.example/callback"],
            GrantTypes = [OAuthGrantTypes.AuthorizationCode],
            ResponseTypes = ["code"],
            TokenEndpointAuthMethod = "none"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_redirect_uri", error.Error);
    }

    [Fact]
    public async Task GetMcpProtectedResourceMetadataAsync_ReturnsMcpResourceMetadata()
    {
        using var client = _server.CreateClient();

        var response = await client.GetAsync("/.well-known/oauth-protected-resource/mcp", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var metadata = await DeserializeResponseAsync<OAuthProtectedResourceMetadata>(response);
        Assert.NotNull(metadata);
        Assert.Equal(Resource, metadata.Resource);
        Assert.Contains("http://localhost:7110", metadata.AuthorizationServers);
        Assert.Contains("header", metadata.BearerMethodsSupported);
        Assert.Contains(AuthorizationRoles.McpRead, metadata.ScopesSupported);
        Assert.Contains(AuthorizationRoles.OfflineAccess, metadata.ScopesSupported);
    }

    [Fact]
    public async Task GetRestApiProtectedResourceMetadataAsync_ReturnsRestApiResourceMetadata()
    {
        using var client = _server.CreateClient();

        var response = await client.GetAsync("/.well-known/oauth-protected-resource/api/v2", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var metadata = await DeserializeResponseAsync<OAuthProtectedResourceMetadata>(response);
        Assert.NotNull(metadata);
        Assert.Equal(RestApiResource, metadata.Resource);
        Assert.Contains("http://localhost:7110", metadata.AuthorizationServers);
        Assert.Contains("header", metadata.BearerMethodsSupported);
        Assert.Contains(AuthorizationRoles.ProjectsRead, metadata.ScopesSupported);
        Assert.DoesNotContain(AuthorizationRoles.McpRead, metadata.ScopesSupported);
    }

    [Fact]
    public async Task McpAsync_WithoutAuth_ReturnsProtectedResourceChallenge()
    {
        using var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}", Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        Assert.Equal("resource_metadata=\"http://localhost:7110/.well-known/oauth-protected-resource/mcp\", scope=\"mcp:read\"", challenge.Parameter);
    }

    [Fact]
    public async Task McpAsync_GetWithoutAuth_ReturnsProtectedResourceChallenge()
    {
        using var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        Assert.Equal("resource_metadata=\"http://localhost:7110/.well-known/oauth-protected-resource/mcp\", scope=\"mcp:read\"", challenge.Parameter);
    }

    [Fact]
    public async Task McpAsync_UntrustedOrigin_ReturnsForbidden()
    {
        // Arrange
        using var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"server/discover\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Origin", "https://attacker.example");

        // Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task McpAsync_CanonicalOrigin_ContinuesToAuthorization()
    {
        // Arrange
        using var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"server/discover\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Origin", "http://localhost:7110");

        // Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RestApiAsync_WithoutAuth_ReturnsProtectedResourceChallenge()
    {
        using var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v2/projects");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        Assert.Equal("resource_metadata=\"http://localhost:7110/.well-known/oauth-protected-resource/api/v2\"", challenge.Parameter);
    }

    [Fact]
    public async Task McpAsync_GetWithUserAuth_ReturnsForbidden()
    {
        using var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SampleDataService.TEST_USER_EMAIL}:{SampleDataService.TEST_USER_PASSWORD}")));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task McpAsync_GetWithOAuthBearer_ReturnsMethodNotAllowed()
    {
        var token = await IssueTokenAsync();
        using var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Theory]
    [InlineData("http://localhost:7110")]
    [InlineData("http://localhost:7110/#!")]
    [InlineData("http://localhost:7110/#")]
    public async Task AuthorizeAsync_SeparateApiOrigin_RedirectsToConfiguredApplicationWithOriginalQuery(string applicationBaseUrl)
    {
        // Arrange
        var options = GetService<AppOptions>();
        string originalBaseUrl = options.BaseURL;
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeRequest(PkceVerifier, authenticate: false);
        request.RequestUri = new Uri(new Uri("http://api.localhost:7110/api/v2/"), request.RequestUri!);
        string query = request.RequestUri.Query;

        try
        {
            options.BaseURL = applicationBaseUrl;

            // Act
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("http://localhost:7110/oauth/authorize" + query, response.Headers.Location?.OriginalString);
        }
        finally
        {
            options.BaseURL = originalBaseUrl;
        }
    }

    [Fact]
    public async Task AuthorizeAsync_AnonymousUser_RedirectsToAuthorizeBridge()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeRequest(PkceVerifier, authenticate: false);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var locationHeader = response.Headers.Location;
        Assert.NotNull(locationHeader);
        string location = locationHeader.ToString();
        Assert.StartsWith("http://localhost:7110/oauth/authorize?", location);
        var bridgeQuery = QueryHelpers.ParseQuery(location[(location.IndexOf('?') + 1)..]);
        Assert.Equal(ClientId, bridgeQuery["client_id"].ToString());
        Assert.Equal("code", bridgeQuery["response_type"].ToString());
        Assert.Equal(RedirectUri, bridgeQuery["redirect_uri"].ToString());
        Assert.Equal(Resource, bridgeQuery["resource"].ToString());
    }

    [Fact]
    public async Task AuthorizeAsync_WithoutResource_RedirectsToAuthorizeBridgeWithoutResource()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeRequest(PkceVerifier, resource: null, authenticate: false);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var locationHeader = response.Headers.Location;
        Assert.NotNull(locationHeader);
        string location = locationHeader.ToString();
        Assert.StartsWith("http://localhost:7110/oauth/authorize?", location);
        var bridgeQuery = QueryHelpers.ParseQuery(location[(location.IndexOf('?') + 1)..]);
        Assert.False(bridgeQuery.ContainsKey("resource"));
    }

    [Fact]
    public async Task AuthorizeAsync_AuthenticatedUser_RedirectsToAuthorizeBridge()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeRequest(PkceVerifier);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("http://localhost:7110/oauth/authorize?", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_ValidRequest_ReturnsRedirectUri()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest(PkceVerifier);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authorization = await DeserializeResponseAsync<OAuthAuthorizeResponse>(response);
        Assert.NotNull(authorization);
        var redirectUri = new Uri(authorization.RedirectUri);
        Assert.Equal(RedirectUri, redirectUri.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(redirectUri.Query);
        Assert.True(query.TryGetValue("code", out var code));
        Assert.False(String.IsNullOrEmpty(code.ToString()));
        Assert.Equal("state-value", query["state"].ToString());
        Assert.Equal("http://localhost:7110", query["iss"].ToString());
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_WithoutOrganizations_ReturnsBadRequest()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest(PkceVerifier, organizationIds: []);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_request", error.Error);
        Assert.Equal("Select at least one organization.", error.ErrorDescription);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("mcp:read projects:read stacks:read events:read")]
    public async Task AuthorizeAsync_SelfRegisteredClient_GrantsEveryApprovedScopeAndKeepsThemOnRefresh(string? registeredScopes)
    {
        // Arrange: a client's own registration, including one saved before stacks:write was a default.
        using var client = CreateHttpClient();
        var registration = await RegisterScopeTestClientAsync(client, registeredScopes);
        string requestedScopes = String.Join(' ', OAuthService.SupportedScopes);

        // Act
        var consent = await GetConsentAsync(client, registration.ClientId, requestedScopes);
        var token = await IssueTokenAsync(clientId: registration.ClientId, scope: requestedScopes, organizationIds: [SampleDataService.TEST_ORG_ID]);

        // Assert
        Assert.Equal(OAuthService.SupportedScopes, consent.Scopes);
        Assert.Contains(AuthorizationRoles.StacksWrite, token.Scope);
        Assert.NotNull(token.RefreshToken);

        // Act: refresh limits a grant to the client's allowed scopes, so it must not strip what was approved.
        using var refreshContent = CreateRefreshTokenContent(token.RefreshToken, registration.ClientId);
        var refreshResponse = await client.PostAsync("oauth/token", refreshContent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshed = await DeserializeResponseAsync<OAuthTokenResponse>(refreshResponse);
        Assert.NotNull(refreshed);
        Assert.Equal(token.Scope, refreshed.Scope);
    }

    [Fact]
    public async Task AuthorizeAsync_AdministratorRestrictedClient_NarrowsConsentAndRejectsDisallowedScopes()
    {
        // Arrange: an administrator saved a read-only list for a client that requests everything discovery advertises.
        using var client = CreateHttpClient();
        var registration = await RegisterScopeTestClientAsync(client, registeredScopes: null);
        string[] restrictedScopes = [AuthorizationRoles.McpRead, AuthorizationRoles.ProjectsRead, AuthorizationRoles.StacksRead, AuthorizationRoles.EventsRead];
        var application = await _oauthApplicationRepository.GetByClientIdAsync(registration.ClientId, o => o.ImmediateConsistency());
        Assert.NotNull(application);
        application.Scopes = restrictedScopes;
        application.UpdatedByUserId = TestConstants.UserId;
        await _oauthApplicationRepository.SaveAsync(application, o => o.ImmediateConsistency());
        string requestedScopes = String.Join(' ', OAuthService.SupportedScopes);

        // Act
        var consent = await GetConsentAsync(client, registration.ClientId, requestedScopes);

        // Assert
        Assert.Equal(restrictedScopes, consent.Scopes);

        // Act: issuing a code stores the submitted scopes, so the final request is never narrowed.
        using var finalRequest = CreateAuthorizeJsonRequest(PkceVerifier, clientId: registration.ClientId, scope: requestedScopes, organizationIds: [SampleDataService.TEST_ORG_ID]);
        var finalResponse = await client.SendAsync(finalRequest, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, finalResponse.StatusCode);
        var error = await finalResponse.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_scope", error.Error);
        Assert.Equal("Scopes not allowed for this application: stacks:write, saved-views:read, saved-views:write, offline_access. Restart authorization with scopes allowed for this application.", error.ErrorDescription);
        Assert.DoesNotContain("redirect_uri", await finalResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Act: approve what consent offered.
        string approvedScopes = String.Join(' ', consent.Scopes);
        var token = await IssueTokenAsync(clientId: registration.ClientId, scope: approvedScopes, organizationIds: [SampleDataService.TEST_ORG_ID]);

        // Assert
        Assert.Null(token.RefreshToken);
        Assert.Equal(approvedScopes, token.Scope);
    }

    private async Task<OAuthClientRegistrationResponse> RegisterScopeTestClientAsync(HttpClient client, string? registeredScopes)
    {
        var response = await client.PostAsJsonAsync("oauth/register", new OAuthClientRegistrationRequest
        {
            ClientName = "Scope Regression Client",
            RedirectUris = [RedirectUri],
            Scope = registeredScopes
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registration = await DeserializeResponseAsync<OAuthClientRegistrationResponse>(response);
        Assert.NotNull(registration);
        return registration;
    }

    private async Task<OAuthAuthorizeConsentResponse> GetConsentAsync(HttpClient client, string clientId, string scope)
    {
        using var request = CreateAuthorizeJsonRequest(PkceVerifier, clientId: clientId, scope: scope, organizationIds: [SampleDataService.TEST_ORG_ID]);
        request.RequestUri = new Uri("oauth/authorize/consent", UriKind.Relative);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var consent = await DeserializeResponseAsync<OAuthAuthorizeConsentResponse>(response);
        Assert.NotNull(consent);
        return consent;
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_ForeignOrganization_ReturnsBadRequestWithoutCode()
    {
        // Arrange
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest(PkceVerifier, organizationIds: [ObjectId.GenerateNewId().ToString()]);

        // Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_request", error.Error);
        Assert.Equal("One or more selected organizations are not available to the current user.", error.ErrorDescription);
        Assert.DoesNotContain("redirect_uri", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_WithoutResource_ReturnsBadRequest()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest(PkceVerifier, resource: null);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_target", error.Error);
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_InvalidRedirectUri_ReturnsBadRequest()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest("bad-verifier", "https://attacker.example/callback");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_request", error.Error);
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_UnknownClient_ReturnsBadRequest()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest("bad-verifier", clientId: "unknown-oauth-client");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_client", error.Error);
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_InvalidResponseType_ReturnsBadRequest()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest("bad-verifier", responseType: "token");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("unsupported_response_type", error.Error);
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_InvalidResource_ReturnsBadRequest()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest("bad-verifier", resource: "http://localhost:7110");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_target", error.Error);
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_InvalidCodeChallenge_ReturnsBadRequest()
    {
        using var client = CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/authorize")
        {
            Content = JsonContent.Create(new OAuthAuthorizeForm
            {
                ClientId = ClientId,
                ResponseType = "code",
                RedirectUri = RedirectUri,
                Scope = $"{AuthorizationRoles.McpRead} {AuthorizationRoles.ProjectsRead}",
                State = "state-value",
                CodeChallenge = "short",
                CodeChallengeMethod = OAuthService.CodeChallengeMethod,
                Resource = Resource
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SampleDataService.TEST_USER_EMAIL}:{SampleDataService.TEST_USER_PASSWORD}")));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_request", error.Error);
    }

    [Fact]
    public async Task GetAuthorizeConsentAsync_ValidRequest_ReturnsValidatedClientDetails()
    {
        const string clientId = "named-oauth-client";
        const string redirectUri = "http://localhost/named-callback";
        await CreateStoredOAuthApplicationAsync(clientId, redirectUri, name: "Named OAuth Client");
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest(
            PkceVerifier,
            redirectUri,
            RestApiResource,
            clientId,
            scope: $"{AuthorizationRoles.ProjectsRead} {AuthorizationRoles.OfflineAccess}",
            organizationIds: []);
        request.RequestUri = new Uri("oauth/authorize/consent", UriKind.Relative);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var consent = await DeserializeResponseAsync<OAuthAuthorizeConsentResponse>(response);
        Assert.NotNull(consent);
        Assert.Equal(clientId, consent.ClientId);
        Assert.Equal("Named OAuth Client", consent.ClientName);
        Assert.Equal(redirectUri, consent.RedirectUri);
        Assert.Equal(RestApiResource, consent.Resource);
        Assert.Equal([AuthorizationRoles.ProjectsRead, AuthorizationRoles.OfflineAccess], consent.Scopes);
        Assert.Empty(consent.RequiredScopes);
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_ClientMetadataDocument_PersistsObservedApplication()
    {
        await CreateAuthorizationCodeAsync(PkceVerifier, MetadataRedirectUri, clientId: MetadataClientId, organizationIds: [SampleDataService.TEST_ORG_ID]);

        var application = await _oauthApplicationRepository.GetByClientIdAsync(MetadataClientId, o => o.ImmediateConsistency());

        Assert.NotNull(application);
        Assert.Equal("Example AI Client", application.Name);
        Assert.Equal(OAuthApplication.SystemUserId, application.CreatedByUserId);
        Assert.Contains(MetadataRedirectUri, application.RedirectUris);
        Assert.Contains(AuthorizationRoles.McpRead, application.Scopes);
        Assert.Contains(SampleDataService.TEST_ORG_ID, application.OrganizationIds);
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_ClientMetadataDocumentWithoutScope_DefaultsToAllScopes()
    {
        await CreateAuthorizationCodeAsync(
            PkceVerifier,
            MetadataRedirectUri,
            clientId: MetadataNoScopeClientId,
            scope: String.Join(' ', OAuthService.DefaultScopes));

        var application = await _oauthApplicationRepository.GetByClientIdAsync(MetadataNoScopeClientId, o => o.ImmediateConsistency());

        Assert.NotNull(application);
        Assert.Contains(AuthorizationRoles.McpRead, application.Scopes);
        Assert.Contains(AuthorizationRoles.ProjectsRead, application.Scopes);
        Assert.Contains(AuthorizationRoles.StacksRead, application.Scopes);
        Assert.Contains(AuthorizationRoles.EventsRead, application.Scopes);
        Assert.Contains(AuthorizationRoles.StacksWrite, application.Scopes);
        Assert.Contains(AuthorizationRoles.OfflineAccess, application.Scopes);
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_ObservedClientMetadataDocumentMissingNewScope_RefreshesScopes()
    {
        await CreateAuthorizationCodeAsync(PkceVerifier, ClaudeLoopbackRedirectUri, clientId: ClaudeMetadataClientId);
        var application = await _oauthApplicationRepository.GetByClientIdAsync(ClaudeMetadataClientId, o => o.ImmediateConsistency());
        Assert.NotNull(application);
        application.Scopes = application.Scopes.Where(s => !String.Equals(s, AuthorizationRoles.StacksWrite, StringComparison.Ordinal)).ToArray();
        await _oauthApplicationRepository.SaveAsync(application, o => o.ImmediateConsistency());

        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest(
            PkceVerifier,
            ClaudeLoopbackRedirectUri,
            clientId: ClaudeMetadataClientId,
            scope: $"{AuthorizationRoles.McpRead} {AuthorizationRoles.ProjectsRead} {AuthorizationRoles.StacksRead} {AuthorizationRoles.StacksWrite} {AuthorizationRoles.EventsRead} {AuthorizationRoles.OfflineAccess}");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authorization = await DeserializeResponseAsync<OAuthAuthorizeResponse>(response);
        Assert.NotNull(authorization);
        var refreshedApplication = await _oauthApplicationRepository.GetByClientIdAsync(ClaudeMetadataClientId, o => o.ImmediateConsistency());
        Assert.NotNull(refreshedApplication);
        Assert.Contains(AuthorizationRoles.StacksWrite, refreshedApplication.Scopes);
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_AdministratorEditedMetadataClient_KeepsRestrictionWhenMetadataDiffers()
    {
        // Arrange: the saved name differs from the metadata document, which would otherwise trigger a refresh.
        await CreateAuthorizationCodeAsync(PkceVerifier, ClaudeLoopbackRedirectUri, clientId: ClaudeMetadataClientId);
        string[] restrictedScopes = [AuthorizationRoles.McpRead, AuthorizationRoles.ProjectsRead];
        var application = await _oauthApplicationRepository.GetByClientIdAsync(ClaudeMetadataClientId, o => o.ImmediateConsistency());
        Assert.NotNull(application);
        application.Name = "Administrator Reviewed Client";
        application.Scopes = restrictedScopes;
        application.UpdatedByUserId = TestConstants.UserId;
        await _oauthApplicationRepository.SaveAsync(application, o => o.ImmediateConsistency());

        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest(
            PkceVerifier,
            ClaudeLoopbackRedirectUri,
            clientId: ClaudeMetadataClientId,
            scope: String.Join(' ', OAuthService.SupportedScopes));

        // Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_scope", error.Error);
        var savedApplication = await _oauthApplicationRepository.GetByClientIdAsync(ClaudeMetadataClientId, o => o.ImmediateConsistency());
        Assert.NotNull(savedApplication);
        Assert.Equal("Administrator Reviewed Client", savedApplication.Name);
        Assert.Equal(restrictedScopes, savedApplication.Scopes);
        Assert.Equal(TestConstants.UserId, savedApplication.UpdatedByUserId);
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_ClientMetadataDocumentLoopbackRedirectUriWithPort_ReturnsRedirectUri()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest(PkceVerifier, ClaudeLoopbackRedirectUri, clientId: ClaudeMetadataClientId);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authorization = await DeserializeResponseAsync<OAuthAuthorizeResponse>(response);
        Assert.NotNull(authorization);
        var redirectUri = new Uri(authorization.RedirectUri);
        Assert.Equal(ClaudeLoopbackRedirectUri, redirectUri.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(redirectUri.Query);
        Assert.True(query.TryGetValue("code", out var code));
        Assert.False(String.IsNullOrEmpty(code.ToString()));
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_InsecureClientMetadataDocument_ReturnsBadRequest()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest("bad-verifier", MetadataRedirectUri, clientId: "http://oauth.example/client.json");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_client", error.Error);
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_ClientMetadataDocumentMismatch_ReturnsBadRequest()
    {
        const string clientId = "https://oauth.example/mismatch.json";
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest("bad-verifier", MetadataRedirectUri, clientId: clientId);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_client", error.Error);

        var application = await _oauthApplicationRepository.GetByClientIdAsync(clientId, o => o.ImmediateConsistency());
        Assert.Null(application);
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_ClientMetadataDocumentMissingName_ReturnsBadRequest()
    {
        // Arrange
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest(PkceVerifier, MetadataRedirectUri, clientId: MetadataMissingNameClientId);

        // Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_client", error.Error);

        var application = await _oauthApplicationRepository.GetByClientIdAsync(MetadataMissingNameClientId, o => o.ImmediateConsistency());
        Assert.Null(application);
    }

    [Fact]
    public async Task TokenAsync_ValidAuthorizationCode_ReturnsOAuthTokens()
    {
        var token = await IssueTokenAsync();

        Assert.NotNull(token);
        Assert.False(String.IsNullOrEmpty(token.AccessToken));
        Assert.Equal(OAuthService.OAuthTokenLength, token.AccessToken.Length);
        Assert.NotNull(token.RefreshToken);
        Assert.Equal(OAuthService.OAuthTokenLength, token.RefreshToken.Length);
        Assert.Equal("Bearer", token.TokenType);
        Assert.Equal(Resource, token.Resource);
        Assert.Contains(AuthorizationRoles.McpRead, token.Scope);

        var storedToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        Assert.NotEqual(token.AccessToken, storedToken.Id);
        Assert.Equal(OAuthService.CreateTokenHash(token.AccessToken), storedToken.AccessTokenHash);
        Assert.Equal(OAuthService.CreateTokenHash(token.RefreshToken), storedToken.RefreshTokenHash);
        Assert.Equal(ClientId, storedToken.ClientId);
        Assert.False(String.IsNullOrWhiteSpace(storedToken.GrantId));
        Assert.Equal(Resource, storedToken.Resource);
        Assert.Contains(AuthorizationRoles.EventsRead, storedToken.Scopes);
        Assert.Equal([TestConstants.OrganizationId], storedToken.OrganizationIds);
    }

    [Fact]
    public async Task TokenAsync_AuthorizationCodeWithReducedScopes_IssuesSelectedScopes()
    {
        var token = await IssueTokenAsync(scope: $"{AuthorizationRoles.McpRead} {AuthorizationRoles.ProjectsRead} {AuthorizationRoles.OfflineAccess}");

        Assert.NotNull(token);
        Assert.Equal($"{AuthorizationRoles.McpRead} {AuthorizationRoles.ProjectsRead} {AuthorizationRoles.OfflineAccess}", token.Scope);
        Assert.NotNull(token.RefreshToken);

        var storedToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        Assert.NotEqual(token.AccessToken, storedToken.Id);
        Assert.Equal(OAuthService.CreateTokenHash(token.AccessToken), storedToken.AccessTokenHash);
        Assert.Equal(OAuthService.CreateTokenHash(token.RefreshToken), storedToken.RefreshTokenHash);
        Assert.Contains(AuthorizationRoles.McpRead, storedToken.Scopes);
        Assert.Contains(AuthorizationRoles.ProjectsRead, storedToken.Scopes);
        Assert.DoesNotContain(AuthorizationRoles.StacksWrite, storedToken.Scopes);
        Assert.DoesNotContain(AuthorizationRoles.EventsRead, storedToken.Scopes);
        Assert.Contains(AuthorizationRoles.OfflineAccess, storedToken.Scopes);
    }

    [Fact]
    public async Task TokenAsync_WithoutResource_ReturnsBadRequest()
    {
        string verifier = PkceVerifier;
        string code = await CreateAuthorizationCodeAsync(verifier);
        using var client = CreateHttpClient();

        var response = await client.PostAsync("oauth/token", CreateTokenExchangeContent(code, verifier, resource: null), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_request", error.Error);
    }

    [Fact]
    public async Task TokenAsync_StoredOAuthApplication_ReturnsOAuthTokens()
    {
        const string clientId = "stored-oauth-client";
        const string redirectUri = "http://localhost/stored-callback";
        await CreateStoredOAuthApplicationAsync(clientId, redirectUri);

        var token = await IssueTokenAsync(clientId, redirectUri);

        Assert.NotNull(token);
        var storedToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        Assert.Equal(clientId, storedToken.ClientId);
        Assert.Contains(AuthorizationRoles.McpRead, storedToken.Scopes);
    }

    [Fact]
    public async Task TokenAsync_ClientMetadataDocumentApplication_ReturnsOAuthTokens()
    {
        var token = await IssueTokenAsync(MetadataClientId, MetadataRedirectUri);

        Assert.NotNull(token);
        var storedToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        Assert.Equal(MetadataClientId, storedToken.ClientId);
        Assert.Contains(AuthorizationRoles.McpRead, storedToken.Scopes);
    }

    [Fact]
    public async Task OAuthBearer_RestApiResourceWithProjectsRead_ReturnsProjects()
    {
        var token = await IssueTokenAsync(resource: RestApiResource, scope: AuthorizationRoles.ProjectsRead);

        await SendRequestAsync(r => r
            .BearerToken(token.AccessToken)
            .AppendPath("projects")
            .StatusCodeShouldBeOk());
    }

    [Fact]
    public async Task OAuthBearer_InternalTokenId_ReturnsUnauthorized()
    {
        var token = await IssueTokenAsync(resource: RestApiResource, scope: AuthorizationRoles.ProjectsRead);
        var storedToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(storedToken);

        await SendRequestAsync(r => r
            .BearerToken(storedToken.Id)
            .AppendPath("projects")
            .StatusCodeShouldBeUnauthorized());
    }


    [Fact]
    public async Task OAuthBearer_McpResourceForRestApi_ReturnsUnauthorized()
    {
        var token = await IssueTokenAsync();

        await SendRequestAsync(r => r
            .BearerToken(token.AccessToken)
            .AppendPath("projects")
            .StatusCodeShouldBeUnauthorized());
    }

    [Fact]
    public async Task OAuthBearer_RestApiResourceForMcp_ReturnsUnauthorized()
    {
        var token = await IssueTokenAsync(resource: RestApiResource, scope: AuthorizationRoles.ProjectsRead);
        using var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OAuthBearer_McpV2Client_UsesNativeStatelessProtocolAndCallsTool()
    {
        const string nativeProtocolVersion = "2026-07-28";
        var token = await IssueTokenAsync();
        using var httpClient = _server.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(_server.BaseAddress, "/mcp"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {token.AccessToken}"
                }
            },
            httpClient,
            GetService<ILoggerFactory>(),
            ownsHttpClient: false);

        var options = new McpClientOptions
        {
            ProtocolVersion = nativeProtocolVersion,
            ClientInfo = new Implementation
            {
                Name = "exceptionless-mcp-v2-tests",
                Version = "1.0.0"
            }
        };

        await using var client = await McpClient.CreateAsync(
            transport,
            options,
            cancellationToken: TestContext.Current.CancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var resolvedProject = await client.CallToolAsync(
            "resolve_project",
            new Dictionary<string, object?>
            {
                ["projectId"] = TestConstants.ProjectId
            },
            cancellationToken: TestContext.Current.CancellationToken);
        var project = await client.CallToolAsync(
            "get_project",
            new Dictionary<string, object?>
            {
                ["projectId"] = TestConstants.ProjectId
            },
            cancellationToken: TestContext.Current.CancellationToken);
        var missingProject = await client.CallToolAsync(
            "get_project",
            new Dictionary<string, object?>
            {
                ["projectId"] = ObjectId.GenerateNewId().ToString()
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(nativeProtocolVersion, client.NegotiatedProtocolVersion);
        Assert.Null(client.SessionId);
        Assert.Contains("stateless", client.ServerInstructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(tools, tool => String.Equals(tool.Name, "resolve_project", StringComparison.Ordinal));
        Assert.DoesNotContain(tools, tool => String.Equals(tool.Name, "get_context", StringComparison.Ordinal));
        Assert.DoesNotContain(tools, tool => String.Equals(tool.Name, "switch_organization", StringComparison.Ordinal));
        Assert.DoesNotContain(tools, tool => String.Equals(tool.Name, "switch_project", StringComparison.Ordinal));
        Assert.DoesNotContain(tools, tool => String.Equals(tool.Name, "resolve_project_context", StringComparison.Ordinal));
        Assert.NotEqual(true, resolvedProject.IsError);
        Assert.NotEqual(true, project.IsError);
        Assert.NotNull(project.StructuredContent);
        Assert.Contains(TestConstants.ProjectId, project.StructuredContent.ToString(), StringComparison.Ordinal);
        Assert.True(missingProject.IsError);
    }

    [Theory]
    [InlineData("2025-06-18")]
    [InlineData("2025-11-25")]
    public async Task OAuthBearer_McpDownLevelClient_NegotiatesAndCallsTool(string downLevelProtocolVersion)
    {
        var token = await IssueTokenAsync();
        using var httpClient = _server.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(_server.BaseAddress, "/mcp"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {token.AccessToken}"
                }
            },
            httpClient,
            GetService<ILoggerFactory>(),
            ownsHttpClient: false);

        var options = new McpClientOptions
        {
            ProtocolVersion = downLevelProtocolVersion,
            ClientInfo = new Implementation
            {
                Name = "exceptionless-mcp-down-level-tests",
                Version = "1.0.0"
            }
        };

        await using var client = await McpClient.CreateAsync(
            transport,
            options,
            cancellationToken: TestContext.Current.CancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var filterFields = await client.CallToolAsync("get_filter_fields", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(downLevelProtocolVersion, client.NegotiatedProtocolVersion);
        Assert.Contains("stateless", client.ServerInstructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(tools, tool => String.Equals(tool.Name, "get_filter_fields", StringComparison.Ordinal));
        Assert.NotEqual(true, filterFields.IsError);
        Assert.NotNull(filterFields.StructuredContent);
    }

    [Fact]
    public async Task OAuthBearer_McpClientSendsRemovedArgument_ReturnsInvalidArgument()
    {
        var token = await IssueTokenAsync();
        using var httpClient = _server.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(_server.BaseAddress, "/mcp"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {token.AccessToken}"
                }
            },
            httpClient,
            GetService<ILoggerFactory>(),
            ownsHttpClient: false);
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);

        // Binding drops unknown arguments, so without this check an old client's last would silently search all time.
        var result = await client.CallToolAsync(
            "search_stacks",
            new Dictionary<string, object?>
            {
                ["projectId"] = TestConstants.ProjectId,
                ["last"] = "24h"
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        var error = result.StructuredContent!.Value.GetProperty("error");
        Assert.Equal("invalid_argument", error.GetProperty("code").GetString());
        Assert.Contains("search_stacks does not accept last.", error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task McpSavedViews_CreateListUpdateDelete_BehaveLikeTheApp()
    {
        // Arrange
        await using var client = await CreateSavedViewMcpClientAsync($"{AuthorizationRoles.SavedViewsRead} {AuthorizationRoles.SavedViewsWrite}");

        // Act
        var created = await CallSavedViewToolAsync(client, "create_saved_view", new()
        {
            ["name"] = "Production checkout errors",
            ["viewType"] = "stacks",
            ["projectIds"] = new[] { TestConstants.ProjectId },
            ["types"] = new[] { "error" },
            ["environments"] = new[] { "Production" },
            ["tags"] = new[] { "needs review" }
        });

        // Assert
        Assert.True(created.GetProperty("ok").GetBoolean(), created.ToString());
        var view = created.GetProperty("data");
        string id = view.GetProperty("id").GetString()!;
        string filter = $"project:{TestConstants.ProjectId} type:error environment:production tag:\"needs review\" (status:open OR status:regressed)";
        Assert.Equal(filter, view.GetProperty("filter").GetString());
        Assert.Equal("[now-7d TO now]", view.GetProperty("time").GetString());
        Assert.True(view.GetProperty("isPrivate").GetBoolean());

        var stored = await GetService<ISavedViewRepository>().GetByIdAsync(id, o => o.Cache(false));
        Assert.NotNull(stored);
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.Equal(user?.Id, stored.UserId);
        Assert.Equal("production-checkout-errors", stored.Slug);
        Assert.Contains("\"hidden\":true", stored.FilterDefinitions);
        Assert.Contains("\"type\":\"date\"", stored.FilterDefinitions);

        // Act
        await RefreshDataAsync();
        var listed = await CallSavedViewToolAsync(client, "list_saved_views", new() { ["viewType"] = "stacks", ["name"] = "CHECKOUT" });

        // Assert
        var match = Assert.Single(listed.GetProperty("data").GetProperty("items").EnumerateArray());
        Assert.Equal(id, match.GetProperty("id").GetString());

        // Act
        var updated = await CallSavedViewToolAsync(client, "update_saved_view", new()
        {
            ["savedViewId"] = id,
            ["name"] = "Staging checkout errors",
            ["environments"] = new[] { "staging" },
            ["clearCriteria"] = new[] { "tag" },
            ["time"] = "[now-30d TO now]"
        });

        // Assert
        Assert.True(updated.GetProperty("data").GetProperty("changed").GetBoolean(), updated.ToString());
        stored = await GetService<ISavedViewRepository>().GetByIdAsync(id, o => o.Cache(false));
        Assert.NotNull(stored);
        Assert.Equal("Staging checkout errors", stored.Name);
        Assert.Equal("production-checkout-errors", stored.Slug);
        Assert.Equal($"project:{TestConstants.ProjectId} type:error environment:staging (status:open OR status:regressed)", stored.Filter);
        Assert.Equal("[now-30d TO now]", stored.Time);
        Assert.Contains("[now-30d TO now]", stored.FilterDefinitions);

        // Act
        var unchanged = await CallSavedViewToolAsync(client, "update_saved_view", new()
        {
            ["savedViewId"] = id,
            ["name"] = "",
            ["time"] = "",
            ["sort"] = "",
            ["types"] = Array.Empty<string>(),
            ["environments"] = new[] { "staging" }
        });

        // Assert
        Assert.False(unchanged.GetProperty("data").GetProperty("changed").GetBoolean(), unchanged.ToString());

        // Act
        var deleted = await CallSavedViewToolAsync(client, "delete_saved_view", new() { ["savedViewId"] = id });

        // Assert
        Assert.True(deleted.GetProperty("ok").GetBoolean(), deleted.ToString());
        Assert.Null(await GetService<ISavedViewRepository>().GetByIdAsync(id, o => o.Cache(false)));
    }

    [Theory]
    [InlineData("list_saved_views", AuthorizationRoles.SavedViewsWrite, AuthorizationRoles.SavedViewsRead)]
    [InlineData("create_saved_view", AuthorizationRoles.SavedViewsRead, AuthorizationRoles.SavedViewsWrite)]
    [InlineData("update_saved_view", AuthorizationRoles.SavedViewsRead, AuthorizationRoles.SavedViewsWrite)]
    [InlineData("delete_saved_view", AuthorizationRoles.SavedViewsRead, AuthorizationRoles.SavedViewsWrite)]
    public async Task McpSavedViews_MissingScope_ReturnsForbidden(string toolName, string grantedScope, string requiredScope)
    {
        // Arrange
        await using var client = await CreateSavedViewMcpClientAsync(grantedScope);
        var arguments = new Dictionary<string, object?>();
        if (toolName == "create_saved_view")
        {
            arguments["name"] = "Errors";
            arguments["viewType"] = "stacks";
        }
        else if (toolName != "list_saved_views")
        {
            arguments["savedViewId"] = ObjectId.GenerateNewId().ToString();
        }

        // Act
        var result = await CallSavedViewToolAsync(client, toolName, arguments);

        // Assert
        Assert.Equal("forbidden", result.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(requiredScope, result.GetProperty("error").GetProperty("details").GetProperty("requiredScope").GetString());
    }

    [Theory]
    [MemberData(nameof(InvalidSavedViews))]
    public async Task McpSavedViews_InvalidArguments_ReturnsErrorWithoutCreating(string expectedCode, Dictionary<string, object?> arguments)
    {
        // Arrange
        await using var client = await CreateSavedViewMcpClientAsync(AuthorizationRoles.SavedViewsWrite);
        long countBefore = await GetService<ISavedViewRepository>().CountByOrganizationIdAsync(TestConstants.OrganizationId);

        // Act
        var result = await CallSavedViewToolAsync(client, "create_saved_view", new(arguments)
        {
            ["name"] = arguments.GetValueOrDefault("name") ?? "Invalid view",
            ["allProjects"] = arguments.GetValueOrDefault("allProjects") ?? !arguments.ContainsKey("projectIds")
        });

        // Assert
        Assert.Equal(expectedCode, result.GetProperty("error").GetProperty("code").GetString());
        await RefreshDataAsync();
        Assert.Equal(countBefore, await GetService<ISavedViewRepository>().CountByOrganizationIdAsync(TestConstants.OrganizationId));
    }

    public static TheoryData<string, Dictionary<string, object?>> InvalidSavedViews => new()
    {
        { "invalid_filter", new() { ["viewType"] = "events", ["query"] = "*timeout*" } },
        { "unknown_filter_field", new() { ["viewType"] = "stacks", ["query"] = "is_critical:true" } },
        { "invalid_argument", new() { ["viewType"] = "stacks", ["allProjects"] = false } },
        { "invalid_argument", new() { ["viewType"] = "stacks", ["projectIds"] = new[] { "" } } },
        { "invalid_time_range", new() { ["viewType"] = "events", ["time"] = "whenever" } },
        { "invalid_time_range", new() { ["viewType"] = "stream", ["time"] = "[now-1d TO now]" } },
        { "invalid_sort", new() { ["viewType"] = "stacks", ["sort"] = "-date" } },
        { "invalid_sort", new() { ["viewType"] = "events", ["sort"] = "-summary" } },
        { "invalid_status", new() { ["viewType"] = "stacks", ["statuses"] = new[] { "closed" } } },
        { "invalid_argument", new() { ["viewType"] = "stacks", ["types"] = new[] { "error OR type:log" } } },
        { "invalid_argument", new() { ["viewType"] = "events", ["tags"] = new[] { "a\" OR tag:\"b" } } },
        { "invalid_argument", new() { ["viewType"] = "dashboard" } },
        { "invalid_argument", new() { ["viewType"] = "sessions", ["types"] = new[] { "error" } } },
        { "invalid_argument", new() { ["viewType"] = "stacks", ["name"] = new string('a', 101) } },
        { "invalid_id", new() { ["viewType"] = "stacks", ["projectIds"] = new[] { "billing" } } },
        { "not_found", new() { ["viewType"] = "stacks", ["projectIds"] = new[] { ObjectId.GenerateNewId().ToString() } } }
    };

    [Fact]
    public async Task McpSavedViews_DuplicateName_ReturnsConflict()
    {
        // Arrange
        await using var client = await CreateSavedViewMcpClientAsync(AuthorizationRoles.SavedViewsWrite);
        var arguments = new Dictionary<string, object?> { ["name"] = "Duplicate view", ["viewType"] = "events", ["allProjects"] = true };
        await CallSavedViewToolAsync(client, "create_saved_view", arguments);
        await RefreshDataAsync();

        // Act
        var result = await CallSavedViewToolAsync(client, "create_saved_view", arguments);

        // Assert
        Assert.Equal("conflict", result.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task McpSavedViews_ViewsOutOfReach_AreNotFound()
    {
        // Arrange
        await using var client = await CreateSavedViewMcpClientAsync($"{AuthorizationRoles.SavedViewsRead} {AuthorizationRoles.SavedViewsWrite}");
        var repository = GetService<ISavedViewRepository>();
        var otherUsersView = await repository.AddAsync(CreateSavedView(TestConstants.OrganizationId, "Someone's private view", ObjectId.GenerateNewId().ToString()), o => o.ImmediateConsistency());
        var otherOrganizationsView = await repository.AddAsync(CreateSavedView(SampleDataService.FREE_ORG_ID, "Another organization's view", userId: null), o => o.ImmediateConsistency());

        // Act
        var listed = await CallSavedViewToolAsync(client, "list_saved_views", new() { ["name"] = "view" });
        var deletedPrivate = await CallSavedViewToolAsync(client, "delete_saved_view", new() { ["savedViewId"] = otherUsersView.Id });
        var updatedPrivate = await CallSavedViewToolAsync(client, "update_saved_view", new() { ["savedViewId"] = otherUsersView.Id, ["name"] = "Taken" });
        var deletedOtherOrganization = await CallSavedViewToolAsync(client, "delete_saved_view", new() { ["savedViewId"] = otherOrganizationsView.Id });

        // Assert
        Assert.DoesNotContain(listed.GetProperty("data").GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetString() == otherUsersView.Id || item.GetProperty("id").GetString() == otherOrganizationsView.Id);
        Assert.Equal("not_found", deletedPrivate.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("not_found", updatedPrivate.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("not_found", deletedOtherOrganization.GetProperty("error").GetProperty("code").GetString());
        Assert.NotNull(await repository.GetByIdAsync(otherUsersView.Id, o => o.Cache(false)));
        Assert.NotNull(await repository.GetByIdAsync(otherOrganizationsView.Id, o => o.Cache(false)));
    }

    [Theory]
    [MemberData(nameof(SeparateCriteriaChanges))]
    public async Task McpSavedViews_FilterOnlyView_RejectsCriteriaChangesInsteadOfCombiningThem(Dictionary<string, object?> change)
    {
        // Arrange
        await using var client = await CreateSavedViewMcpClientAsync(AuthorizationRoles.SavedViewsWrite);
        var repository = GetService<ISavedViewRepository>();
        var view = CreateSavedView(TestConstants.OrganizationId, "Filter only", userId: null);
        view.Filter = $"project:{TestConstants.ProjectId} status:open";
        view = await repository.AddAsync(view, o => o.ImmediateConsistency());

        // Act
        var result = await CallSavedViewToolAsync(client, "update_saved_view", new(change) { ["savedViewId"] = view.Id });

        // Assert
        Assert.Equal("invalid_argument", result.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains(view.Filter!, result.GetProperty("error").GetProperty("message").GetString(), StringComparison.Ordinal);
        var stored = await repository.GetByIdAsync(view.Id, o => o.Cache(false));
        Assert.Equal(view.Filter, stored?.Filter);
    }

    public static TheoryData<Dictionary<string, object?>> SeparateCriteriaChanges => new()
    {
        new Dictionary<string, object?> { ["allProjects"] = true },
        new Dictionary<string, object?> { ["projectIds"] = new[] { TestConstants.ProjectId } },
        new Dictionary<string, object?> { ["statuses"] = new[] { "fixed" } },
        new Dictionary<string, object?> { ["clearCriteria"] = new[] { "status" } },
        new Dictionary<string, object?> { ["query"] = "status:fixed" }
    };

    [Fact]
    public async Task McpSavedViews_FilterOnlyView_QueryReplacesTheWholeFilter()
    {
        // Arrange
        await using var client = await CreateSavedViewMcpClientAsync(AuthorizationRoles.SavedViewsWrite);
        var view = CreateSavedView(TestConstants.OrganizationId, "Filter only", userId: null);
        view.Filter = $"project:{TestConstants.ProjectId} status:open";
        view = await GetService<ISavedViewRepository>().AddAsync(view, o => o.ImmediateConsistency());

        // Act
        var result = await CallSavedViewToolAsync(client, "update_saved_view", new() { ["savedViewId"] = view.Id, ["query"] = "status:fixed", ["clearCriteria"] = new[] { "query" } });

        // Assert
        Assert.True(result.GetProperty("ok").GetBoolean(), result.ToString());
        Assert.Equal("status:fixed", result.GetProperty("data").GetProperty("filter").GetString());
    }

    [Fact]
    public async Task McpSavedViews_CreateWithStackField_AcceptsFieldsTheStackFilterResolves()
    {
        // Arrange
        await using var client = await CreateSavedViewMcpClientAsync(AuthorizationRoles.SavedViewsWrite);

        // Act
        var result = await CallSavedViewToolAsync(client, "create_saved_view", new()
        {
            ["name"] = "Critical errors",
            ["viewType"] = "stacks",
            ["projectIds"] = new[] { TestConstants.ProjectId },
            ["query"] = "critical:true data.region:us"
        });

        // Assert
        Assert.True(result.GetProperty("ok").GetBoolean(), result.ToString());
        Assert.EndsWith("critical:true data.region:us", result.GetProperty("data").GetProperty("filter").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task McpSavedViews_ListByExactNameOrId_ReturnsOnlyThatView()
    {
        // Arrange
        await using var client = await CreateSavedViewMcpClientAsync(AuthorizationRoles.SavedViewsRead);
        var repository = GetService<ISavedViewRepository>();
        var view = await repository.AddAsync(CreateSavedView(TestConstants.OrganizationId, "Production errors", userId: null), o => o.ImmediateConsistency());
        await repository.AddAsync(CreateSavedView(TestConstants.OrganizationId, "Production errors weekly", userId: null), o => o.ImmediateConsistency());

        // Act
        var byName = await CallSavedViewToolAsync(client, "list_saved_views", new() { ["name"] = "production ERRORS" });
        var byId = await CallSavedViewToolAsync(client, "list_saved_views", new() { ["name"] = view.Id });
        var byText = await CallSavedViewToolAsync(client, "list_saved_views", new() { ["name"] = "production" });

        // Assert
        Assert.Equal(view.Id, Assert.Single(byName.GetProperty("data").GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
        Assert.Equal(view.Id, Assert.Single(byId.GetProperty("data").GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
        Assert.Equal(2, byText.GetProperty("data").GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task McpSavedViews_CreateWithSearch_KeepsItLastSoAnOrCannotWidenTheOtherCriteria()
    {
        // Arrange
        await using var client = await CreateSavedViewMcpClientAsync(AuthorizationRoles.SavedViewsWrite);

        // Act
        var result = await CallSavedViewToolAsync(client, "create_saved_view", new()
        {
            ["name"] = "Search view",
            ["viewType"] = "stacks",
            ["projectIds"] = new[] { TestConstants.ProjectId },
            ["query"] = "type:error OR type:log"
        });

        // Assert
        Assert.Equal(
            $"project:{TestConstants.ProjectId} (status:open OR status:regressed) type:error OR type:log",
            result.GetProperty("data").GetProperty("filter").GetString());
    }

    [Fact]
    public async Task McpSavedViews_CreateThenList_ShowsTheViewWithoutWaiting()
    {
        // Arrange
        await using var client = await CreateSavedViewMcpClientAsync($"{AuthorizationRoles.SavedViewsRead} {AuthorizationRoles.SavedViewsWrite}");
        var arguments = new Dictionary<string, object?> { ["name"] = "Fresh view", ["viewType"] = "events", ["allProjects"] = true };

        // Act
        var created = await CallSavedViewToolAsync(client, "create_saved_view", arguments);
        var listed = await CallSavedViewToolAsync(client, "list_saved_views", new() { ["name"] = "Fresh view" });
        var duplicate = await CallSavedViewToolAsync(client, "create_saved_view", arguments);
        var deleted = await CallSavedViewToolAsync(client, "delete_saved_view", new() { ["savedViewId"] = created.GetProperty("data").GetProperty("id").GetString() });
        var recreated = await CallSavedViewToolAsync(client, "create_saved_view", arguments);

        // Assert
        Assert.Single(listed.GetProperty("data").GetProperty("items").EnumerateArray());
        Assert.Equal("conflict", duplicate.GetProperty("error").GetProperty("code").GetString());
        Assert.True(deleted.GetProperty("ok").GetBoolean(), deleted.ToString());
        Assert.True(recreated.GetProperty("ok").GetBoolean(), recreated.ToString());
    }

    [Theory]
    [MemberData(nameof(InvalidSavedViewUpdates))]
    public async Task McpSavedViews_InvalidUpdate_ReturnsErrorWithoutChanging(string expectedCode, Dictionary<string, object?> change)
    {
        // Arrange
        await using var client = await CreateSavedViewMcpClientAsync(AuthorizationRoles.SavedViewsWrite);
        var repository = GetService<ISavedViewRepository>();
        await repository.AddAsync(CreateSavedView(TestConstants.OrganizationId, "Taken name", userId: null), o => o.ImmediateConsistency());
        var view = await repository.AddAsync(CreateSavedView(TestConstants.OrganizationId, "Original name", userId: null), o => o.ImmediateConsistency());

        // Act
        var result = await CallSavedViewToolAsync(client, "update_saved_view", new(change) { ["savedViewId"] = view.Id });

        // Assert
        Assert.Equal(expectedCode, result.GetProperty("error").GetProperty("code").GetString());
        var stored = await repository.GetByIdAsync(view.Id, o => o.Cache(false));
        Assert.Equal("Original name", stored?.Name);
    }

    public static TheoryData<string, Dictionary<string, object?>> InvalidSavedViewUpdates => new()
    {
        { "conflict", new() { ["name"] = "Taken name" } },
        { "invalid_argument", new() { ["clearCriteria"] = new[] { "project" } } },
        { "invalid_time_range", new() { ["time"] = "whenever" } },
        { "invalid_sort", new() { ["sort"] = "-summary" } },
        { "invalid_filter", new() { ["query"] = "*timeout*" } },
        { "not_found", new() { ["projectIds"] = new[] { SampleDataService.FREE_PROJECT_ID } } }
    };

    [Fact]
    public async Task McpSavedViews_ClearingTheLastCriteria_LeavesNoFilter()
    {
        // Arrange
        await using var client = await CreateSavedViewMcpClientAsync(AuthorizationRoles.SavedViewsWrite);
        var created = await CallSavedViewToolAsync(client, "create_saved_view", new() { ["name"] = "Only status", ["viewType"] = "sessions", ["allProjects"] = true, ["statuses"] = new[] { "open" } });

        // Act
        var updated = await CallSavedViewToolAsync(client, "update_saved_view", new() { ["savedViewId"] = created.GetProperty("data").GetProperty("id").GetString(), ["clearCriteria"] = new[] { "status" } });

        // Assert
        Assert.True(updated.GetProperty("ok").GetBoolean(), updated.ToString());
        Assert.False(updated.GetProperty("data").TryGetProperty("filter", out var filter) && filter.ValueKind != System.Text.Json.JsonValueKind.Null, updated.ToString());
    }

    [Fact]
    public async Task McpSavedViews_SharedViewFromAnotherMember_CanBeChangedAndDeleted()
    {
        // Arrange
        await using var client = await CreateSavedViewMcpClientAsync(AuthorizationRoles.SavedViewsWrite);
        var repository = GetService<ISavedViewRepository>();
        var view = await repository.AddAsync(CreateSavedView(TestConstants.OrganizationId, "Team view", userId: null), o => o.ImmediateConsistency());

        // Act
        var updated = await CallSavedViewToolAsync(client, "update_saved_view", new() { ["savedViewId"] = view.Id, ["name"] = "Team view renamed" });
        var deleted = await CallSavedViewToolAsync(client, "delete_saved_view", new() { ["savedViewId"] = view.Id });

        // Assert
        Assert.Equal("Team view renamed", updated.GetProperty("data").GetProperty("name").GetString());
        Assert.True(deleted.GetProperty("ok").GetBoolean(), deleted.ToString());
        Assert.Null(await repository.GetByIdAsync(view.Id, o => o.Cache(false)));
    }

    [Fact]
    public async Task McpSavedViews_OrganizationOutsideTheGrant_IsOutOfReach()
    {
        // Arrange: the user belongs to the second organization, but this token was not granted it.
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(user);
        user.OrganizationIds.Add(SampleDataService.FREE_ORG_ID);
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency().Cache());
        await using var client = await CreateSavedViewMcpClientAsync($"{AuthorizationRoles.SavedViewsRead} {AuthorizationRoles.SavedViewsWrite}");
        var repository = GetService<ISavedViewRepository>();
        var view = await repository.AddAsync(CreateSavedView(SampleDataService.FREE_ORG_ID, "Outside the grant", userId: null), o => o.ImmediateConsistency());

        // Act
        var listed = await CallSavedViewToolAsync(client, "list_saved_views", new() { ["organizationId"] = SampleDataService.FREE_ORG_ID });
        var created = await CallSavedViewToolAsync(client, "create_saved_view", new() { ["name"] = "Should not exist", ["viewType"] = "events", ["allProjects"] = true, ["organizationId"] = SampleDataService.FREE_ORG_ID });
        var deleted = await CallSavedViewToolAsync(client, "delete_saved_view", new() { ["savedViewId"] = view.Id });

        // Assert
        Assert.False(listed.GetProperty("ok").GetBoolean(), listed.ToString());
        Assert.False(created.GetProperty("ok").GetBoolean(), created.ToString());
        Assert.False(deleted.GetProperty("ok").GetBoolean(), deleted.ToString());
        Assert.NotNull(await repository.GetByIdAsync(view.Id, o => o.Cache(false)));
    }

    private SavedView CreateSavedView(string organizationId, string name, string? userId)
    {
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        return new SavedView
        {
            OrganizationId = organizationId,
            UserId = userId,
            CreatedByUserId = TestConstants.UserId,
            Name = name,
            Slug = name.ToLowerInvariant().Replace("'", String.Empty).Replace(' ', '-'),
            ViewType = "events",
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };
    }

    private async Task<McpClient> CreateSavedViewMcpClientAsync(string scopes)
    {
        await SetStoredOAuthApplicationScopesAsync(ClientId, AuthorizationRoles.McpRead, AuthorizationRoles.SavedViewsRead, AuthorizationRoles.SavedViewsWrite);
        var token = await IssueTokenAsync(scope: $"{AuthorizationRoles.McpRead} {scopes}");
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(_server.BaseAddress, "/mcp"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {token.AccessToken}"
                }
            },
            _server.CreateClient(),
            GetService<ILoggerFactory>(),
            ownsHttpClient: true);

        return await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static async Task<System.Text.Json.JsonElement> CallSavedViewToolAsync(McpClient client, string toolName, Dictionary<string, object?> arguments)
    {
        var result = await client.CallToolAsync(toolName, arguments, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(result.StructuredContent);
        return result.StructuredContent.Value;
    }

    [Fact]
    public async Task OAuthBearer_RestApiResourceMissingScope_ReturnsForbidden()
    {
        var token = await IssueTokenAsync(resource: RestApiResource, scope: AuthorizationRoles.ProjectsRead);

        await SendRequestAsync(r => r
            .BearerToken(token.AccessToken)
            .AppendPath("events")
            .StatusCodeShouldBeForbidden());
    }

    [Fact]
    public async Task OAuthBearer_McpResourceMissingRequiredScope_ReturnsInsufficientScopeChallenge()
    {
        // Arrange
        var token = await IssueTokenAsync();
        var storedToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        storedToken.Scopes = [AuthorizationRoles.ProjectsRead, AuthorizationRoles.OfflineAccess];
        await _oauthTokenRepository.SaveAsync(storedToken, o => o.ImmediateConsistency());

        using var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        // Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        Assert.Equal("error=\"insufficient_scope\", scope=\"mcp:read\", resource_metadata=\"http://localhost:7110/.well-known/oauth-protected-resource/mcp\"", challenge.Parameter);
    }

    [Fact]
    public async Task OAuthBearer_RestApiResourceUsesSelectedOrganizations()
    {
        var token = await IssueTokenAsync(resource: RestApiResource, scope: AuthorizationRoles.ProjectsRead, organizationIds: [TestConstants.OrganizationId]);
        var user = await GetService<IUserRepository>().GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(user);
        string unselectedOrganizationId = Assert.Single(user.OrganizationIds, id => !String.Equals(id, TestConstants.OrganizationId, StringComparison.Ordinal));

        await SendRequestAsync(r => r
            .BearerToken(token.AccessToken)
            .AppendPath($"organizations/{TestConstants.OrganizationId}/projects")
            .StatusCodeShouldBeOk());

        await SendRequestAsync(r => r
            .BearerToken(token.AccessToken)
            .AppendPath($"organizations/{unselectedOrganizationId}/projects")
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public async Task OAuthBearer_RemovedOrganizationAccess_ReturnsUnauthorizedAndDisablesToken()
    {
        var token = await IssueTokenAsync(resource: RestApiResource, scope: AuthorizationRoles.ProjectsRead, organizationIds: [TestConstants.OrganizationId]);

        await RemoveTestUserFromOrganizationAsync(TestConstants.OrganizationId);

        await SendRequestAsync(r => r
            .BearerToken(token.AccessToken)
            .AppendPath("projects")
            .StatusCodeShouldBeUnauthorized());

        var storedToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        Assert.True(storedToken.IsDisabled);
        Assert.Null(storedToken.RefreshTokenHash);
    }

    [Fact]
    public async Task OAuthBearer_DisabledStoredOAuthApplication_ReturnsUnauthorized()
    {
        const string clientId = "stored-disabled-client";
        const string redirectUri = "http://localhost/stored-disabled-callback";
        var application = await CreateStoredOAuthApplicationAsync(clientId, redirectUri);
        var token = await IssueTokenAsync(clientId, redirectUri, RestApiResource, AuthorizationRoles.ProjectsRead);

        application.IsDisabled = true;
        await _oauthApplicationRepository.SaveAsync(application, o => o.ImmediateConsistency());

        await SendRequestAsync(r => r
            .BearerToken(token.AccessToken)
            .AppendPath("projects")
            .StatusCodeShouldBeUnauthorized());
    }

    [Fact]
    public async Task OAuthBearer_DisabledStoredOAuthApplicationAfterCachedAccess_ReturnsUnauthorized()
    {
        const string clientId = "cached-disabled-client";
        const string redirectUri = "http://localhost/cached-disabled-callback";
        var application = await CreateStoredOAuthApplicationAsync(clientId, redirectUri);
        var token = await IssueTokenAsync(clientId, redirectUri, RestApiResource, AuthorizationRoles.ProjectsRead);

        await SendRequestAsync(r => r
            .BearerToken(token.AccessToken)
            .AppendPath("projects")
            .StatusCodeShouldBeOk());

        await SendRequestAsync(r => r
            .Put()
            .AsGlobalAdminUser()
            .AppendPaths("admin", "oauth-applications", application.Id)
            .Content(new UpdateOAuthApplication
            {
                ClientId = clientId,
                Name = application.Name,
                RedirectUris = application.RedirectUris,
                Scopes = application.Scopes,
                Notes = application.Notes,
                IsDisabled = true
            })
            .StatusCodeShouldBeOk());

        await SendRequestAsync(r => r
            .BearerToken(token.AccessToken)
            .AppendPath("projects")
            .StatusCodeShouldBeUnauthorized());
    }

    [Fact]
    public async Task TokenAsync_InvalidCodeVerifier_ReturnsBadRequestAndConsumesCode()
    {
        string verifier = PkceVerifier;
        string code = await CreateAuthorizationCodeAsync(verifier);
        using var client = CreateHttpClient();

        var response = await client.PostAsync("oauth/token", CreateTokenExchangeContent(code, WrongPkceVerifier), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_grant", error.Error);

        response = await client.PostAsync("oauth/token", CreateTokenExchangeContent(code, verifier), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TokenAsync_InvalidCodeVerifierShape_ReturnsBadRequestWithoutConsumingCode()
    {
        string code = await CreateAuthorizationCodeAsync(PkceVerifier);
        using var client = CreateHttpClient();

        var response = await client.PostAsync("oauth/token", CreateTokenExchangeContent(code, "short-verifier"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_grant", error.Error);

        response = await client.PostAsync("oauth/token", CreateTokenExchangeContent(code, PkceVerifier), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TokenAsync_RefreshToken_RotatesRefreshToken()
    {
        var token = await IssueTokenAsync();
        Assert.NotNull(token.RefreshToken);
        using var client = CreateHttpClient();

        using var refreshRequestContent = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["grant_type"] = OAuthGrantTypes.RefreshToken,
            ["client_id"] = ClientId,
            ["refresh_token"] = token.RefreshToken
        });
        var refreshResponse = await client.PostAsync("oauth/token", refreshRequestContent, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshedToken = await DeserializeResponseAsync<OAuthTokenResponse>(refreshResponse);
        Assert.NotNull(refreshedToken);
        Assert.NotNull(refreshedToken.RefreshToken);
        Assert.NotEqual(token.AccessToken, refreshedToken.AccessToken);
        Assert.NotEqual(token.RefreshToken, refreshedToken.RefreshToken);

        var spentToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(spentToken);
        Assert.True(spentToken.IsDisabled);
        Assert.Equal(OAuthService.CreateTokenHash(token.RefreshToken), spentToken.RefreshTokenHash);
        Assert.NotNull(spentToken.RefreshTokenUsedUtc);

        var refreshedStoredToken = await GetStoredOAuthTokenAsync(refreshedToken.AccessToken);
        Assert.NotNull(refreshedStoredToken);
        Assert.False(refreshedStoredToken.IsDisabled);
        Assert.Equal(OAuthService.CreateTokenHash(refreshedToken.RefreshToken), refreshedStoredToken.RefreshTokenHash);
        Assert.Equal(spentToken.GrantId, refreshedStoredToken.GrantId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TokenAsync_ExpandedClientScopes_RequiresFreshConsent(bool originalOfflineAccess)
    {
        // Arrange
        string originalScopes = originalOfflineAccess ? "mcp:read projects:read offline_access" : "mcp:read projects:read";
        await SetStoredOAuthApplicationScopesAsync(ClientId, originalScopes.Split(' '));
        var token = await IssueTokenAsync(scope: originalScopes);
        Assert.Equal(originalOfflineAccess, token.RefreshToken is not null);

        // Act
        await SetStoredOAuthApplicationScopesAsync(ClientId, OAuthService.SupportedScopes.ToArray());

        // Assert
        var storedToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        Assert.DoesNotContain(AuthorizationRoles.StacksWrite, storedToken.Scopes);
        Assert.Equal(originalOfflineAccess, storedToken.RefreshTokenHash is not null);
        if (originalOfflineAccess)
        {
            using var client = CreateHttpClient();
            using var content = CreateRefreshTokenContent(token.RefreshToken);
            // Act
            var response = await client.PostAsync("oauth/token", content, TestContext.Current.CancellationToken);
            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var refreshed = await DeserializeResponseAsync<OAuthTokenResponse>(response);
            Assert.NotNull(refreshed);
            Assert.Equal(originalScopes, refreshed.Scope);
        }

        // Act
        var freshToken = await IssueTokenAsync(scope: String.Join(' ', OAuthService.SupportedScopes));
        // Assert
        Assert.Contains(AuthorizationRoles.StacksWrite, freshToken.Scope);
        Assert.NotNull(freshToken.RefreshToken);
    }

    [Fact]
    public async Task TokenAsync_RecentlyUsedRefreshToken_IssuesNewToken()
    {
        var token = await IssueTokenAsync();
        Assert.NotNull(token.RefreshToken);
        using var client = CreateHttpClient();

        using var firstRefreshContent = CreateRefreshTokenContent(token.RefreshToken);
        var firstRefreshResponse = await client.PostAsync("oauth/token", firstRefreshContent, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstRefreshResponse.StatusCode);
        var firstRefreshedToken = await DeserializeResponseAsync<OAuthTokenResponse>(firstRefreshResponse);
        Assert.NotNull(firstRefreshedToken);

        using var reusedRefreshContent = CreateRefreshTokenContent(token.RefreshToken);
        var reusedRefreshResponse = await client.PostAsync("oauth/token", reusedRefreshContent, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, reusedRefreshResponse.StatusCode);
        var secondRefreshedToken = await DeserializeResponseAsync<OAuthTokenResponse>(reusedRefreshResponse);
        Assert.NotNull(secondRefreshedToken);
        Assert.NotEqual(firstRefreshedToken.AccessToken, secondRefreshedToken.AccessToken);
        Assert.NotEqual(firstRefreshedToken.RefreshToken, secondRefreshedToken.RefreshToken);

        var spentToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        var firstRefreshedStoredToken = await GetStoredOAuthTokenAsync(firstRefreshedToken.AccessToken);
        var secondRefreshedStoredToken = await GetStoredOAuthTokenAsync(secondRefreshedToken.AccessToken);
        Assert.NotNull(spentToken);
        Assert.NotNull(firstRefreshedStoredToken);
        Assert.NotNull(secondRefreshedStoredToken);
        Assert.True(spentToken.IsDisabled);
        Assert.False(firstRefreshedStoredToken.IsDisabled);
        Assert.False(secondRefreshedStoredToken.IsDisabled);
        Assert.Equal(spentToken.GrantId, firstRefreshedStoredToken.GrantId);
        Assert.Equal(spentToken.GrantId, secondRefreshedStoredToken.GrantId);
    }

    [Fact]
    public async Task TokenAsync_MultipleGenerationOldRefreshTokenWithinGracePeriod_IssuesNewToken()
    {
        var token = await IssueTokenAsync();
        Assert.NotNull(token.RefreshToken);
        using var client = CreateHttpClient();

        using var firstRefreshContent = CreateRefreshTokenContent(token.RefreshToken);
        var firstRefreshResponse = await client.PostAsync("oauth/token", firstRefreshContent, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstRefreshResponse.StatusCode);
        var firstRefreshedToken = await DeserializeResponseAsync<OAuthTokenResponse>(firstRefreshResponse);
        Assert.NotNull(firstRefreshedToken);
        Assert.NotNull(firstRefreshedToken.RefreshToken);

        using var secondRefreshContent = CreateRefreshTokenContent(firstRefreshedToken.RefreshToken);
        var secondRefreshResponse = await client.PostAsync("oauth/token", secondRefreshContent, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, secondRefreshResponse.StatusCode);

        using var staleRefreshContent = CreateRefreshTokenContent(token.RefreshToken);
        var staleRefreshResponse = await client.PostAsync("oauth/token", staleRefreshContent, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, staleRefreshResponse.StatusCode);
        var staleRefreshedToken = await DeserializeResponseAsync<OAuthTokenResponse>(staleRefreshResponse);
        Assert.NotNull(staleRefreshedToken);
        Assert.NotNull(staleRefreshedToken.RefreshToken);
    }

    [Fact]
    public async Task TokenAsync_RefreshTokenReusedOutsideGracePeriod_RevokesGrantFamily()
    {
        var token = await IssueTokenAsync();
        Assert.NotNull(token.RefreshToken);
        using var client = CreateHttpClient();

        using var firstRefreshContent = CreateRefreshTokenContent(token.RefreshToken);
        var firstRefreshResponse = await client.PostAsync("oauth/token", firstRefreshContent, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstRefreshResponse.StatusCode);

        TimeProvider.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(1)));

        using var reusedRefreshContent = CreateRefreshTokenContent(token.RefreshToken);
        var reusedRefreshResponse = await client.PostAsync("oauth/token", reusedRefreshContent, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, reusedRefreshResponse.StatusCode);
        await AssertGrantFamilyRevokedAsync(token.AccessToken);
    }

    [Fact]
    public async Task TokenAsync_LegacySpentRefreshTokenWithoutUsedTimestamp_RevokesGrantFamily()
    {
        var token = await IssueTokenAsync();
        Assert.NotNull(token.RefreshToken);
        var storedToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        storedToken.IsDisabled = true;
        storedToken.RefreshTokenUsedUtc = null;
        await _oauthTokenRepository.SaveAsync(storedToken, o => o.ImmediateConsistency());
        using var client = CreateHttpClient();

        using var refreshContent = CreateRefreshTokenContent(token.RefreshToken);
        var refreshResponse = await client.PostAsync("oauth/token", refreshContent, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, refreshResponse.StatusCode);
        await AssertGrantFamilyRevokedAsync(token.AccessToken);
    }

    [Fact]
    public async Task TokenAsync_RefreshToken_UsesCurrentOrganizationMembership()
    {
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(user);
        var originalOrganizationIds = user.OrganizationIds.ToArray();
        string removedOrganizationId = Assert.Single(originalOrganizationIds, id => !String.Equals(id, TestConstants.OrganizationId, StringComparison.Ordinal));
        var token = await IssueTokenAsync(
            resource: RestApiResource,
            scope: $"{AuthorizationRoles.ProjectsRead} {AuthorizationRoles.OfflineAccess}",
            organizationIds: originalOrganizationIds);
        Assert.NotNull(token.RefreshToken);

        await RemoveTestUserFromOrganizationAsync(removedOrganizationId);
        using var client = CreateHttpClient();
        using var refreshRequestContent = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["grant_type"] = OAuthGrantTypes.RefreshToken,
            ["client_id"] = ClientId,
            ["refresh_token"] = token.RefreshToken
        });

        var refreshResponse = await client.PostAsync("oauth/token", refreshRequestContent, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshedToken = await DeserializeResponseAsync<OAuthTokenResponse>(refreshResponse);
        Assert.NotNull(refreshedToken);
        var storedToken = await GetStoredOAuthTokenAsync(refreshedToken.AccessToken);
        Assert.NotNull(storedToken);
        Assert.Equal([TestConstants.OrganizationId], storedToken.OrganizationIds);
    }

    [Fact]
    public async Task TokenAsync_RefreshTokenWithRemovedOptionalClientScopes_IssuesReducedScopes()
    {
        var token = await IssueTokenAsync(
            resource: RestApiResource,
            scope: $"{AuthorizationRoles.ProjectsRead} {AuthorizationRoles.EventsRead} {AuthorizationRoles.OfflineAccess}");
        Assert.NotNull(token.RefreshToken);
        await SetStoredOAuthApplicationScopesAsync(ClientId, AuthorizationRoles.ProjectsRead, AuthorizationRoles.OfflineAccess);

        using var client = CreateHttpClient();
        using var refreshRequestContent = CreateRefreshTokenContent(token.RefreshToken);
        var refreshResponse = await client.PostAsync("oauth/token", refreshRequestContent, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshedToken = await DeserializeResponseAsync<OAuthTokenResponse>(refreshResponse);
        Assert.NotNull(refreshedToken);
        Assert.NotNull(refreshedToken.RefreshToken);
        Assert.Equal($"{AuthorizationRoles.ProjectsRead} {AuthorizationRoles.OfflineAccess}", refreshedToken.Scope);

        var refreshedStoredToken = await GetStoredOAuthTokenAsync(refreshedToken.AccessToken);
        Assert.NotNull(refreshedStoredToken);
        Assert.Equal(2, refreshedStoredToken.Scopes.Count);
        Assert.Contains(AuthorizationRoles.ProjectsRead, refreshedStoredToken.Scopes);
        Assert.Contains(AuthorizationRoles.OfflineAccess, refreshedStoredToken.Scopes);
        Assert.DoesNotContain(AuthorizationRoles.EventsRead, refreshedStoredToken.Scopes);
    }

    [Fact]
    public async Task TokenAsync_RefreshTokenWithRemovedOfflineAccess_RevokesGrantFamily()
    {
        var token = await IssueTokenAsync(
            resource: RestApiResource,
            scope: $"{AuthorizationRoles.ProjectsRead} {AuthorizationRoles.OfflineAccess}");
        await SetStoredOAuthApplicationScopesAsync(ClientId, AuthorizationRoles.ProjectsRead);

        await AssertRefreshFailsAndRevokesGrantFamilyAsync(token);
    }

    [Fact]
    public async Task TokenAsync_RefreshTokenWithLargeGrantFamily_RevokesAllTokens()
    {
        var token = await IssueTokenAsync(
            resource: RestApiResource,
            scope: $"{AuthorizationRoles.ProjectsRead} {AuthorizationRoles.OfflineAccess}");
        var storedToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        string grantId = storedToken.GrantId;
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var familyTokens = Enumerable.Range(0, 1005)
            .Select(i => new OAuthToken
            {
                Id = ObjectId.GenerateNewId().ToString(),
                UserId = storedToken.UserId,
                ClientId = storedToken.ClientId,
                GrantId = grantId,
                Resource = storedToken.Resource,
                AccessTokenHash = OAuthService.CreateTokenHash(StringExtensions.GetRandomString(OAuthService.OAuthTokenLength)),
                RefreshTokenHash = OAuthService.CreateTokenHash(StringExtensions.GetRandomString(OAuthService.OAuthTokenLength)),
                Scopes = [AuthorizationRoles.ProjectsRead, AuthorizationRoles.OfflineAccess],
                OrganizationIds = [TestConstants.OrganizationId],
                ExpiresUtc = utcNow.AddHours(1),
                RefreshExpiresUtc = utcNow.AddDays(30),
                IsDisabled = false,
                CreatedBy = storedToken.UserId,
                CreatedUtc = utcNow,
                UpdatedUtc = utcNow
            })
            .ToArray();
        await _oauthTokenRepository.AddAsync(familyTokens, o => o.ImmediateConsistency());
        await SetStoredOAuthApplicationScopesAsync(ClientId, AuthorizationRoles.ProjectsRead);

        await AssertRefreshFailsAndRevokesGrantFamilyAsync(token);

        var results = await _oauthTokenRepository.GetByGrantIdForUpdateAsync(grantId, o => o.ImmediateConsistency().SearchAfterPaging().PageLimit(1000));
        int tokenCount = 0;
        do
        {
            foreach (var familyToken in results.Documents)
            {
                tokenCount++;
                Assert.True(familyToken.IsDisabled);
                Assert.Null(familyToken.RefreshTokenHash);
            }
        } while (await results.NextPageAsync());

        Assert.Equal(familyTokens.Length + 1, tokenCount);
    }

    [Fact]
    public async Task TokenAsync_RefreshTokenWithRemovedRequiredResourceScope_RevokesGrantFamily()
    {
        var token = await IssueTokenAsync(scope: $"{AuthorizationRoles.McpRead} {AuthorizationRoles.ProjectsRead} {AuthorizationRoles.OfflineAccess}");
        await SetStoredOAuthApplicationScopesAsync(ClientId, AuthorizationRoles.ProjectsRead, AuthorizationRoles.OfflineAccess);

        await AssertRefreshFailsAndRevokesGrantFamilyAsync(token);
    }

    [Fact]
    public async Task TokenAsync_RefreshTokenWithoutRemainingResourceScope_RevokesGrantFamily()
    {
        var token = await IssueTokenAsync(
            resource: RestApiResource,
            scope: $"{AuthorizationRoles.ProjectsRead} {AuthorizationRoles.OfflineAccess}");
        await SetStoredOAuthApplicationScopesAsync(ClientId, AuthorizationRoles.OfflineAccess);

        await AssertRefreshFailsAndRevokesGrantFamilyAsync(token);
    }

    [Fact]
    public async Task TokenAsync_ConcurrentRefreshTokenUse_AllowsBothRefreshesWithinGracePeriod()
    {
        var token = await IssueTokenAsync();
        Assert.NotNull(token.RefreshToken);
        using var client = CreateHttpClient();
        using var firstRefreshContent = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["grant_type"] = OAuthGrantTypes.RefreshToken,
            ["client_id"] = ClientId,
            ["refresh_token"] = token.RefreshToken
        });
        using var secondRefreshContent = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["grant_type"] = OAuthGrantTypes.RefreshToken,
            ["client_id"] = ClientId,
            ["refresh_token"] = token.RefreshToken
        });

        var responses = await Task.WhenAll(
            client.PostAsync("oauth/token", firstRefreshContent, TestContext.Current.CancellationToken),
            client.PostAsync("oauth/token", secondRefreshContent, TestContext.Current.CancellationToken));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }

    [Fact]
    public async Task RevokeAsync_DisablesOAuthToken()
    {
        var token = await IssueTokenAsync();
        using var client = CreateHttpClient();

        using var revokeContent = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["client_id"] = ClientId,
            ["token"] = token.AccessToken
        });
        var response = await client.PostAsync("oauth/revoke", revokeContent, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var storedToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        Assert.True(storedToken.IsDisabled);
        Assert.Null(storedToken.RefreshTokenHash);
    }

    [Fact]
    public async Task RevokeAsync_DuplicateTokenFields_UsesFirstValue()
    {
        // Arrange
        var token = await IssueTokenAsync();
        using var client = CreateHttpClient();
        using var revokeContent = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("client_id", ClientId),
            new KeyValuePair<string, string>("token", token.AccessToken),
            new KeyValuePair<string, string>("token", "not-the-issued-token")
        ]);

        // Act
        var response = await client.PostAsync("oauth/revoke", revokeContent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var storedToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        Assert.True(storedToken.IsDisabled);
        Assert.Null(storedToken.RefreshTokenHash);
    }

    [Fact]
    public async Task RevokeAsync_WhitespaceFirstTokenValue_IgnoresSubsequentValue()
    {
        // Arrange
        var token = await IssueTokenAsync();
        using var client = CreateHttpClient();
        using var revokeContent = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("client_id", ClientId),
            new KeyValuePair<string, string>("token", "   "),
            new KeyValuePair<string, string>("token", token.AccessToken)
        ]);

        // Act
        var response = await client.PostAsync("oauth/revoke", revokeContent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var storedToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        Assert.False(storedToken.IsDisabled);
        Assert.NotNull(storedToken.RefreshTokenHash);
    }

    [Fact]
    public async Task RevokeAsync_WithDifferentClientId_DoesNotDisableOAuthToken()
    {
        var token = await IssueTokenAsync();
        using var client = CreateHttpClient();

        using var revokeContent = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["client_id"] = "other-oauth-client",
            ["token"] = token.AccessToken
        });
        var response = await client.PostAsync("oauth/revoke", revokeContent, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var storedToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        Assert.False(storedToken.IsDisabled);
        Assert.NotNull(storedToken.RefreshTokenHash);
    }

    [Fact]
    public async Task OAuthBearer_WithRootResource_ReturnsUnauthorized()
    {
        var token = await IssueTokenAsync(resource: RestApiResource, scope: AuthorizationRoles.ProjectsRead);
        var storedToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        storedToken.Resource = "http://localhost:7110";
        await _oauthTokenRepository.SaveAsync(storedToken, o => o.ImmediateConsistency());

        await SendRequestAsync(r => r
            .BearerToken(token.AccessToken)
            .AppendPath("projects")
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task TokenAsync_AuthorizationCodeCache_KeepsExistingKeyFormat()
    {
        // Arrange
        string code = await CreateAuthorizationCodeAsync(PkceVerifier);
        string cacheKey = $"oauth:code:{code}";
        var cache = GetService<ICacheClient>();
        Assert.True((await cache.GetAsync<OAuthAuthorizationCode>(cacheKey)).HasValue);
        using var client = CreateHttpClient();
        using var exchangeContent = CreateTokenExchangeContent(code, PkceVerifier);

        // Act
        using var response = await client.PostAsync("oauth/token", exchangeContent, TestCancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await cache.GetAsync<OAuthAuthorizationCode>(cacheKey)).HasValue);
    }

    [Fact]
    public async Task TokenAsync_ConcurrentAuthorizationCodeUse_OnlyOneSucceeds()
    {
        // Arrange
        string code = await CreateAuthorizationCodeAsync(PkceVerifier);
        using var client = CreateHttpClient();
        using var firstExchangeContent = CreateTokenExchangeContent(code, PkceVerifier);
        using var secondExchangeContent = CreateTokenExchangeContent(code, PkceVerifier);

        // Act
        var responses = await Task.WhenAll(
            client.PostAsync("oauth/token", firstExchangeContent, TestCancellationToken),
            client.PostAsync("oauth/token", secondExchangeContent, TestCancellationToken));

        // Assert
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest));
        var failedResponse = responses.Single(r => r.StatusCode == HttpStatusCode.BadRequest);
        var error = await failedResponse.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_grant", error.Error);
    }

    [Fact]
    public async Task TokenAsync_AccountGenerationChanged_RejectsExistingCodesAndRefreshTokens()
    {
        // Arrange
        var issued = await IssueTokenAsync(resource: RestApiResource, scope: $"{AuthorizationRoles.ProjectsRead} {AuthorizationRoles.OfflineAccess}");
        string code = await CreateAuthorizationCodeAsync(PkceVerifier);
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(user);
        user.AuthenticationVersion = StringExtensions.GetNewToken();
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency());
        using var client = CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "projects");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", issued.AccessToken);
        using var exchange = CreateTokenExchangeContent(code, PkceVerifier);
        using var refresh = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = OAuthGrantTypes.RefreshToken,
            ["client_id"] = ClientId,
            ["refresh_token"] = issued.RefreshToken!
        });

        // Act
        using var accessResponse = await client.SendAsync(request, TestCancellationToken);
        using var codeResponse = await client.PostAsync("oauth/token", exchange, TestCancellationToken);
        using var refreshResponse = await client.PostAsync("oauth/token", refresh, TestCancellationToken);
        var stored = await GetStoredOAuthTokenAsync(issued.AccessToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, accessResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, codeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, refreshResponse.StatusCode);
        Assert.NotNull(stored);
        Assert.True(stored.IsDisabled);
    }

    private async Task RemoveTestUserFromOrganizationAsync(string organizationId)
    {
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(user);
        user.OrganizationIds.Remove(organizationId);
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency());
    }

    private async Task<OAuthToken?> GetStoredOAuthTokenAsync(string accessToken)
    {
        var results = await _oauthTokenRepository.GetByAccessTokenHashAsync(OAuthService.CreateTokenHash(accessToken), o => o.ImmediateConsistency());
        return results.Documents.FirstOrDefault();
    }

    private async Task<OAuthTokenResponse> IssueTokenAsync(string clientId = ClientId, string redirectUri = RedirectUri, string? resource = Resource, string? scope = null, IReadOnlyCollection<string>? organizationIds = null)
    {
        string verifier = PkceVerifier;
        string code = await CreateAuthorizationCodeAsync(verifier, redirectUri, resource, clientId, scope, organizationIds);
        using var client = CreateHttpClient();
        var response = await client.PostAsync("oauth/token", CreateTokenExchangeContent(code, verifier, redirectUri, resource, clientId), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var token = await DeserializeResponseAsync<OAuthTokenResponse>(response);
        Assert.NotNull(token);
        return token;
    }

    private async Task<string> CreateAuthorizationCodeAsync(string verifier, string redirectUri = RedirectUri, string? resource = Resource, string clientId = ClientId, string? scope = null, IReadOnlyCollection<string>? organizationIds = null)
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest(verifier, redirectUri, resource, clientId, scope: scope, organizationIds: organizationIds);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authorization = await DeserializeResponseAsync<OAuthAuthorizeResponse>(response);
        Assert.NotNull(authorization);
        var redirect = new Uri(authorization.RedirectUri);
        var query = QueryHelpers.ParseQuery(redirect.Query);
        Assert.True(query.TryGetValue("code", out var code));
        Assert.Equal("state-value", query["state"].ToString());
        return code.ToString();
    }

    private async Task<OAuthApplication> CreateStoredOAuthApplicationAsync(string clientId, string redirectUri, bool isDisabled = false, string? name = null)
    {
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var application = new OAuthApplication
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ClientId = clientId,
            Name = name ?? clientId,
            RedirectUris = [redirectUri],
            Scopes = [AuthorizationRoles.McpRead, AuthorizationRoles.ProjectsRead, AuthorizationRoles.StacksRead, AuthorizationRoles.EventsRead, AuthorizationRoles.OfflineAccess],
            Notes = null,
            IsDisabled = isDisabled,
            CreatedByUserId = TestConstants.UserId,
            UpdatedByUserId = TestConstants.UserId,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };

        await _oauthApplicationRepository.AddAsync(application, o => o.ImmediateConsistency());
        return application;
    }

    private async Task SetStoredOAuthApplicationScopesAsync(string clientId, params string[] scopes)
    {
        var application = await _oauthApplicationRepository.GetByClientIdAsync(clientId, o => o.ImmediateConsistency());
        Assert.NotNull(application);
        application.Scopes = scopes;
        application.UpdatedUtc = TimeProvider.GetUtcNow().UtcDateTime;
        await _oauthApplicationRepository.SaveAsync(application, o => o.ImmediateConsistency());
    }

    private async Task AssertRefreshFailsAndRevokesGrantFamilyAsync(OAuthTokenResponse token)
    {
        Assert.NotNull(token.RefreshToken);
        using var client = CreateHttpClient();
        using var refreshRequestContent = CreateRefreshTokenContent(token.RefreshToken);
        var refreshResponse = await client.PostAsync("oauth/token", refreshRequestContent, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, refreshResponse.StatusCode);
        await AssertGrantFamilyRevokedAsync(token.AccessToken);
    }

    private async Task AssertGrantFamilyRevokedAsync(string accessToken)
    {
        var storedToken = await GetStoredOAuthTokenAsync(accessToken);
        Assert.NotNull(storedToken);
        Assert.True(storedToken.IsDisabled);
        Assert.Null(storedToken.RefreshTokenHash);
    }

    private static FormUrlEncodedContent CreateRefreshTokenContent(string? refreshToken, string clientId = ClientId)
    {
        return new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["grant_type"] = OAuthGrantTypes.RefreshToken,
            ["client_id"] = clientId,
            ["refresh_token"] = refreshToken
        });
    }

    private static FormUrlEncodedContent CreateTokenExchangeContent(string code, string verifier, string redirectUri = RedirectUri, string? resource = Resource, string clientId = ClientId)
    {
        var form = new Dictionary<string, string?>
        {
            ["grant_type"] = OAuthGrantTypes.AuthorizationCode,
            ["client_id"] = clientId,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = verifier
        };

        if (resource is not null)
            form["resource"] = resource;

        return new FormUrlEncodedContent(form);
    }

    private static HttpRequestMessage CreateAuthorizeJsonRequest(string verifier, string redirectUri = RedirectUri, string? resource = Resource, string clientId = ClientId, string responseType = "code", string? scope = null, IReadOnlyCollection<string>? organizationIds = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "oauth/authorize")
        {
            Content = JsonContent.Create(new OAuthAuthorizeForm
            {
                ClientId = clientId,
                ResponseType = responseType,
                RedirectUri = redirectUri,
                Scope = scope ?? $"{AuthorizationRoles.McpRead} {AuthorizationRoles.ProjectsRead} {AuthorizationRoles.StacksRead} {AuthorizationRoles.EventsRead} {AuthorizationRoles.OfflineAccess}",
                State = "state-value",
                CodeChallenge = OAuthService.CreateCodeChallenge(verifier),
                CodeChallengeMethod = OAuthService.CodeChallengeMethod,
                Resource = resource,
                OrganizationIds = organizationIds?.ToArray() ?? [TestConstants.OrganizationId]
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SampleDataService.TEST_USER_EMAIL}:{SampleDataService.TEST_USER_PASSWORD}")));
        return request;
    }

    private static HttpRequestMessage CreateAuthorizeRequest(string verifier, string redirectUri = RedirectUri, string? resource = Resource, string clientId = ClientId, bool authenticate = true)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["scope"] = $"{AuthorizationRoles.McpRead} {AuthorizationRoles.ProjectsRead} {AuthorizationRoles.StacksRead} {AuthorizationRoles.EventsRead} {AuthorizationRoles.OfflineAccess}",
            ["state"] = "state-value",
            ["code_challenge"] = OAuthService.CreateCodeChallenge(verifier),
            ["code_challenge_method"] = OAuthService.CodeChallengeMethod
        };

        if (resource is not null)
            query["resource"] = resource;

        string url = QueryHelpers.AddQueryString("oauth/authorize", query);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (authenticate)
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SampleDataService.TEST_USER_EMAIL}:{SampleDataService.TEST_USER_PASSWORD}")));

        return request;
    }

    private sealed class FakeOAuthClientMetadataService : IOAuthClientMetadataService
    {
        public Task<OAuthClientMetadataDocument?> GetClientMetadataAsync(string clientId)
        {
            return Task.FromResult(clientId switch
            {
                MetadataNoScopeClientId => new OAuthClientMetadataDocument
                {
                    ClientId = MetadataNoScopeClientId,
                    ClientName = "No Scope AI Client",
                    RedirectUris = [MetadataRedirectUri],
                    GrantTypes = [OAuthGrantTypes.AuthorizationCode],
                    ResponseTypes = ["code"],
                    TokenEndpointAuthMethod = "none"
                },
                MetadataClientId => new OAuthClientMetadataDocument
                {
                    ClientId = MetadataClientId,
                    ClientName = "Example AI Client",
                    RedirectUris = [MetadataRedirectUri],
                    GrantTypes = [OAuthGrantTypes.AuthorizationCode],
                    ResponseTypes = ["code"],
                    Scope = String.Join(' ', OAuthService.SupportedScopes),
                    TokenEndpointAuthMethod = "none"
                },
                MetadataMissingNameClientId => new OAuthClientMetadataDocument
                {
                    ClientId = MetadataMissingNameClientId,
                    RedirectUris = [MetadataRedirectUri],
                    GrantTypes = [OAuthGrantTypes.AuthorizationCode],
                    ResponseTypes = ["code"],
                    Scope = String.Join(' ', OAuthService.SupportedScopes),
                    TokenEndpointAuthMethod = "none"
                },
                ClaudeMetadataClientId => new OAuthClientMetadataDocument
                {
                    ClientId = ClaudeMetadataClientId,
                    ClientName = "Claude Code",
                    RedirectUris = ["http://localhost/callback", "http://127.0.0.1/callback"],
                    GrantTypes = [OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken],
                    ResponseTypes = ["code"],
                    Scope = String.Join(' ', OAuthService.SupportedScopes),
                    TokenEndpointAuthMethod = "none"
                },
                "https://oauth.example/mismatch.json" => new OAuthClientMetadataDocument
                {
                    ClientId = "https://oauth.example/other-client.json",
                    ClientName = "Mismatched AI Client",
                    RedirectUris = [MetadataRedirectUri],
                    GrantTypes = [OAuthGrantTypes.AuthorizationCode],
                    ResponseTypes = ["code"],
                    Scope = String.Join(' ', OAuthService.SupportedScopes),
                    TokenEndpointAuthMethod = "none"
                },
                _ => null
            });
        }
    }
}
