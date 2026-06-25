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
    private const string MetadataRedirectUri = "https://oauth.example/callback";
    private const string ClaudeMetadataClientId = "https://claude.ai/oauth/claude-code-client-metadata";
    private const string ClaudeLoopbackRedirectUri = "http://localhost:48272/callback";
    private const string Resource = "http://localhost/mcp";

    private readonly IOAuthApplicationRepository _oauthApplicationRepository;
    private readonly ITokenRepository _tokenRepository;

    public OAuthControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _oauthApplicationRepository = GetService<IOAuthApplicationRepository>();
        _tokenRepository = GetService<ITokenRepository>();
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
        Assert.Equal("http://localhost", metadata.Issuer);
        Assert.Equal("http://localhost/api/v2/oauth/authorize", metadata.AuthorizationEndpoint);
        Assert.Equal("http://localhost/api/v2/oauth/token", metadata.TokenEndpoint);
        Assert.Equal("http://localhost/api/v2/oauth/register", metadata.RegistrationEndpoint);
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
    public async Task GetProtectedResourceMetadataAsync_ReturnsMcpResourceMetadata()
    {
        using var client = _server.CreateClient();

        var response = await client.GetAsync("/.well-known/oauth-protected-resource", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var metadata = await DeserializeResponseAsync<OAuthProtectedResourceMetadata>(response);
        Assert.NotNull(metadata);
        Assert.Equal(Resource, metadata.Resource);
        Assert.Contains("http://localhost", metadata.AuthorizationServers);
        Assert.Contains("header", metadata.BearerMethodsSupported);
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
        Assert.Equal("resource_metadata=\"http://localhost/.well-known/oauth-protected-resource\"", challenge.Parameter);
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
        Assert.Equal("resource_metadata=\"http://localhost/.well-known/oauth-protected-resource\"", challenge.Parameter);
    }

    [Fact]
    public async Task McpAsync_GetWithAuth_ReturnsMethodNotAllowed()
    {
        using var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SampleDataService.TEST_USER_EMAIL}:{SampleDataService.TEST_USER_PASSWORD}")));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task AuthorizeAsync_AnonymousUser_RedirectsToAuthorizeBridge()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeRequest("valid-test-code-verifier", authenticate: false);

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
    public async Task AuthorizeAsync_WithoutResource_RedirectsToAuthorizeBridgeWithDefaultResource()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeRequest("valid-test-code-verifier", resource: null, authenticate: false);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var locationHeader = response.Headers.Location;
        Assert.NotNull(locationHeader);
        string location = locationHeader.ToString();
        Assert.StartsWith("/next/oauth/authorize?", location);
        var bridgeQuery = QueryHelpers.ParseQuery(location[(location.IndexOf('?') + 1)..]);
        Assert.Equal(Resource, bridgeQuery["resource"].ToString());
    }

    [Fact]
    public async Task AuthorizeAsync_AuthenticatedUser_RedirectsToAuthorizeBridge()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeRequest("valid-test-code-verifier");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/next/oauth/authorize?", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_ValidRequest_ReturnsRedirectUri()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest("valid-test-code-verifier");

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
    public async Task CompleteAuthorizeAsync_WithoutResource_ReturnsRedirectUri()
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest("valid-test-code-verifier", resource: null);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authorization = await DeserializeResponseAsync<OAuthAuthorizeResponse>(response);
        Assert.NotNull(authorization);
        var redirectUri = new Uri(authorization.RedirectUri);
        Assert.Equal(RedirectUri, redirectUri.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(redirectUri.Query);
        Assert.True(query.TryGetValue("code", out var code));
        Assert.False(String.IsNullOrEmpty(code.ToString()));
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
        using var request = CreateAuthorizeJsonRequest("bad-verifier", resource: "http://localhost");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_target", error.Error);
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_ClientMetadataDocument_PersistsObservedApplication()
    {
        await CreateAuthorizationCodeAsync("valid-test-code-verifier", MetadataRedirectUri, clientId: MetadataClientId);

        var application = await _oauthApplicationRepository.GetByClientIdAsync(MetadataClientId, o => o.ImmediateConsistency());

        Assert.NotNull(application);
        Assert.Equal("Example AI Client", application.Name);
        Assert.Equal(OAuthApplication.SystemUserId, application.CreatedByUserId);
        Assert.Contains(MetadataRedirectUri, application.RedirectUris);
        Assert.Contains(AuthorizationRoles.McpRead, application.Scopes);
    }

    [Fact]
    public async Task CompleteAuthorizeAsync_ObservedClientMetadataDocumentMissingNewScope_RefreshesScopes()
    {
        await CreateAuthorizationCodeAsync("valid-test-code-verifier", ClaudeLoopbackRedirectUri, clientId: ClaudeMetadataClientId);
        var application = await _oauthApplicationRepository.GetByClientIdAsync(ClaudeMetadataClientId, o => o.ImmediateConsistency());
        Assert.NotNull(application);
        application.Scopes = application.Scopes.Where(s => !String.Equals(s, AuthorizationRoles.StacksWrite, StringComparison.Ordinal)).ToArray();
        await _oauthApplicationRepository.SaveAsync(application, o => o.ImmediateConsistency());

        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest(
            "valid-test-code-verifier",
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
        using var request = CreateAuthorizeJsonRequest("valid-test-code-verifier", ClaudeLoopbackRedirectUri, clientId: ClaudeMetadataClientId);

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
        Assert.False(String.IsNullOrEmpty(token.RefreshToken));
        Assert.Equal("Bearer", token.TokenType);
        Assert.Equal(Resource, token.Resource);
        Assert.Contains(AuthorizationRoles.McpRead, token.Scope);

        var storedToken = await _tokenRepository.GetByIdAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        Assert.Equal(TokenType.Access, storedToken.Type);
        Assert.Equal(OAuthTokenType.Access, storedToken.OAuthType);
        Assert.Equal(ClientId, storedToken.OAuthClientId);
        Assert.Equal(Resource, storedToken.OAuthResource);
        Assert.Contains(AuthorizationRoles.EventsRead, storedToken.Scopes);
    }

    [Fact]
    public async Task TokenAsync_WithoutResource_ReturnsOAuthTokens()
    {
        string verifier = "valid-test-code-verifier";
        string code = await CreateAuthorizationCodeAsync(verifier);
        using var client = CreateHttpClient();

        var response = await client.PostAsync("oauth/token", CreateTokenExchangeContent(code, verifier, resource: null), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await DeserializeResponseAsync<OAuthTokenResponse>(response);
        Assert.NotNull(token);
        Assert.Equal(Resource, token.Resource);
    }

    [Fact]
    public async Task TokenAsync_StoredOAuthApplication_ReturnsOAuthTokens()
    {
        const string clientId = "stored-oauth-client";
        const string redirectUri = "http://localhost/stored-callback";
        await CreateStoredOAuthApplicationAsync(clientId, redirectUri);

        var token = await IssueTokenAsync(clientId, redirectUri);

        Assert.NotNull(token);
        var storedToken = await _tokenRepository.GetByIdAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        Assert.Equal(clientId, storedToken.OAuthClientId);
        Assert.Contains(AuthorizationRoles.McpRead, storedToken.Scopes);
    }

    [Fact]
    public async Task TokenAsync_ClientMetadataDocumentApplication_ReturnsOAuthTokens()
    {
        var token = await IssueTokenAsync(MetadataClientId, MetadataRedirectUri);

        Assert.NotNull(token);
        var storedToken = await _tokenRepository.GetByIdAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        Assert.Equal(MetadataClientId, storedToken.OAuthClientId);
        Assert.Contains(AuthorizationRoles.McpRead, storedToken.Scopes);
    }

    [Fact]
    public async Task OAuthBearer_DisabledStoredOAuthApplication_ReturnsUnauthorized()
    {
        const string clientId = "stored-disabled-client";
        const string redirectUri = "http://localhost/stored-disabled-callback";
        var application = await CreateStoredOAuthApplicationAsync(clientId, redirectUri);
        var token = await IssueTokenAsync(clientId, redirectUri);

        application.IsDisabled = true;
        await _oauthApplicationRepository.SaveAsync(application, o => o.ImmediateConsistency());

        await SendRequestAsync(r => r
            .BearerToken(token.AccessToken)
            .AppendPath("users/me")
            .StatusCodeShouldBeUnauthorized());
    }

    [Fact]
    public async Task TokenAsync_InvalidCodeVerifier_ReturnsBadRequestAndConsumesCode()
    {
        string verifier = "valid-test-code-verifier";
        string code = await CreateAuthorizationCodeAsync(verifier);
        using var client = CreateHttpClient();

        var response = await client.PostAsync("oauth/token", CreateTokenExchangeContent(code, "wrong-verifier"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.DeserializeAsync<OAuthErrorResponse>(ensureSuccess: false);
        Assert.NotNull(error);
        Assert.Equal("invalid_grant", error.Error);

        response = await client.PostAsync("oauth/token", CreateTokenExchangeContent(code, verifier), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
        Assert.NotEqual(token.AccessToken, refreshedToken.AccessToken);
        Assert.NotEqual(token.RefreshToken, refreshedToken.RefreshToken);

        using var reusedRefreshRequestContent = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["grant_type"] = OAuthGrantTypes.RefreshToken,
            ["client_id"] = ClientId,
            ["refresh_token"] = token.RefreshToken
        });
        var reusedRefreshResponse = await client.PostAsync("oauth/token", reusedRefreshRequestContent, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, reusedRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task RevokeAsync_DisablesOAuthToken()
    {
        var token = await IssueTokenAsync();
        using var client = CreateHttpClient();

        using var revokeContent = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["token"] = token.AccessToken
        });
        var response = await client.PostAsync("oauth/revoke", revokeContent, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var storedToken = await _tokenRepository.GetByIdAsync(token.AccessToken);
        Assert.NotNull(storedToken);
        Assert.True(storedToken.IsDisabled);
        Assert.Null(storedToken.Refresh);
    }

    [Fact]
    public async Task OAuthBearer_WithRootResource_ReturnsUnauthorized()
    {
        var token = await IssueTokenAsync();
        var storedToken = await _tokenRepository.GetByIdAsync(token.AccessToken, o => o.ImmediateConsistency());
        Assert.NotNull(storedToken);
        storedToken.OAuthResource = "http://localhost";
        await _tokenRepository.SaveAsync(storedToken, o => o.ImmediateConsistency());

        await SendRequestAsync(r => r
            .BearerToken(token.AccessToken)
            .AppendPath("users/me")
            .StatusCodeShouldBeUnauthorized()
        );
    }

    private async Task<OAuthTokenResponse> IssueTokenAsync(string clientId = ClientId, string redirectUri = RedirectUri)
    {
        string verifier = "valid-test-code-verifier";
        string code = await CreateAuthorizationCodeAsync(verifier, redirectUri, clientId: clientId);
        using var client = CreateHttpClient();
        var response = await client.PostAsync("oauth/token", CreateTokenExchangeContent(code, verifier, redirectUri, clientId: clientId), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var token = await DeserializeResponseAsync<OAuthTokenResponse>(response);
        Assert.NotNull(token);
        return token;
    }

    private async Task<string> CreateAuthorizationCodeAsync(string verifier, string redirectUri = RedirectUri, string? resource = Resource, string clientId = ClientId)
    {
        using var client = CreateHttpClient();
        using var request = CreateAuthorizeJsonRequest(verifier, redirectUri, resource, clientId);

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

    private async Task<OAuthApplication> CreateStoredOAuthApplicationAsync(string clientId, string redirectUri, bool isDisabled = false)
    {
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var application = new OAuthApplication
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ClientId = clientId,
            Name = clientId,
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

    private static HttpRequestMessage CreateAuthorizeJsonRequest(string verifier, string redirectUri = RedirectUri, string? resource = Resource, string clientId = ClientId, string responseType = "code", string? scope = null)
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
                Resource = resource
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
