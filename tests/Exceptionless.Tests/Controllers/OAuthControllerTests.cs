using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models.Admin;
using Exceptionless.Web.Models.OAuth;
using FluentRest;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class OAuthControllerTests : IntegrationTestsBase
{
    private const string ClientId = "test-oauth-client";
    private const string RedirectUri = "http://localhost/callback";
    private const string MetadataClientId = "https://oauth.example/client.json";
    private const string MetadataNoScopeClientId = "https://oauth.example/no-scope-client.json";
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

    public OAuthControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
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
    public async Task RegisterAsync_WithoutScope_DefaultsToReadOnlyScopes()
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
        Assert.Contains(AuthorizationRoles.StacksRead, registration.Scope);
        Assert.Contains(AuthorizationRoles.EventsRead, registration.Scope);
        Assert.DoesNotContain(AuthorizationRoles.StacksWrite, registration.Scope);
        Assert.Contains(AuthorizationRoles.OfflineAccess, registration.Scope);

        var application = await _oauthApplicationRepository.GetByClientIdAsync(registration.ClientId, o => o.ImmediateConsistency());
        Assert.NotNull(application);
        Assert.Contains(AuthorizationRoles.McpRead, application.Scopes);
        Assert.Contains(AuthorizationRoles.ProjectsRead, application.Scopes);
        Assert.Contains(AuthorizationRoles.StacksRead, application.Scopes);
        Assert.Contains(AuthorizationRoles.EventsRead, application.Scopes);
        Assert.DoesNotContain(AuthorizationRoles.StacksWrite, application.Scopes);
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
        Assert.Equal("resource_metadata=\"http://localhost:7110/.well-known/oauth-protected-resource/mcp\"", challenge.Parameter);
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
        Assert.Equal("resource_metadata=\"http://localhost:7110/.well-known/oauth-protected-resource/mcp\"", challenge.Parameter);
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
    public async Task AuthorizeAsync_AnonymousUser_RedirectsToAuthorizeBridge()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeRequest(PkceVerifier, authenticate: false);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var locationHeader = response.Headers.Location;
        Assert.NotNull(locationHeader);
        string location = locationHeader.ToString();
        Assert.StartsWith("/next/oauth/authorize?", location);
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
        Assert.StartsWith("/next/oauth/authorize?", location);
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
        Assert.StartsWith("/next/oauth/authorize?", response.Headers.Location.ToString());
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
        await CreateAuthorizationCodeAsync(PkceVerifier, MetadataRedirectUri, clientId: MetadataClientId);

        var application = await _oauthApplicationRepository.GetByClientIdAsync(MetadataClientId, o => o.ImmediateConsistency());

        Assert.NotNull(application);
        Assert.Equal("Example AI Client", application.Name);
        Assert.Equal(OAuthApplication.SystemUserId, application.CreatedByUserId);
        Assert.Contains(MetadataRedirectUri, application.RedirectUris);
        Assert.Contains(AuthorizationRoles.McpRead, application.Scopes);
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_ClientMetadataDocumentWithoutScope_DefaultsToReadOnlyScopes()
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
        Assert.DoesNotContain(AuthorizationRoles.StacksWrite, application.Scopes);
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
    public async Task OAuthBearer_RestApiResourceMissingScope_ReturnsForbidden()
    {
        var token = await IssueTokenAsync(resource: RestApiResource, scope: AuthorizationRoles.ProjectsRead);

        await SendRequestAsync(r => r
            .BearerToken(token.AccessToken)
            .AppendPath("events")
            .StatusCodeShouldBeForbidden());
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

        var refreshedStoredToken = await GetStoredOAuthTokenAsync(refreshedToken.AccessToken);
        Assert.NotNull(refreshedStoredToken);
        Assert.False(refreshedStoredToken.IsDisabled);
        Assert.Equal(OAuthService.CreateTokenHash(refreshedToken.RefreshToken), refreshedStoredToken.RefreshTokenHash);
        Assert.Equal(spentToken.GrantId, refreshedStoredToken.GrantId);

        using var reusedRefreshRequestContent = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["grant_type"] = OAuthGrantTypes.RefreshToken,
            ["client_id"] = ClientId,
            ["refresh_token"] = token.RefreshToken
        });
        var reusedRefreshResponse = await client.PostAsync("oauth/token", reusedRefreshRequestContent, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, reusedRefreshResponse.StatusCode);

        spentToken = await GetStoredOAuthTokenAsync(token.AccessToken);
        refreshedStoredToken = await GetStoredOAuthTokenAsync(refreshedToken.AccessToken);
        Assert.NotNull(spentToken);
        Assert.NotNull(refreshedStoredToken);
        Assert.True(spentToken.IsDisabled);
        Assert.True(refreshedStoredToken.IsDisabled);
        Assert.Null(spentToken.RefreshTokenHash);
        Assert.Null(refreshedStoredToken.RefreshTokenHash);
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
    public async Task TokenAsync_ConcurrentRefreshTokenUse_AllowsOnlyOneRefresh()
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

        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.BadRequest);
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
