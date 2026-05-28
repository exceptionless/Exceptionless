using System.Net;
using System.Text;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Hubs;
using Foundatio.Messaging;
using Foundatio.Repositories.Models;
using Xunit;

namespace Exceptionless.Tests.Hubs;

/// <summary>
/// Integration tests for the SSE endpoint (/api/v2/push).
/// These test the full HTTP pipeline including auth, middleware, and message delivery.
/// </summary>
public sealed class SseIntegrationTests : IntegrationTestsBase
{
    private readonly IMessagePublisher _messagePublisher;

    public SseIntegrationTests(ITestOutputHelper output, AppWebHostFactory factory)
        : base(output, factory)
    {
        _messagePublisher = GetService<IMessagePublisher>();
    }

    [Fact]
    public async Task ConnectWithValidToken_ReturnsEventStream()
    {
        var token = await CreateTokenAsync();

        using var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v2/push");
        request.Headers.Add("Accept", "text/event-stream");
        request.Headers.Add("Authorization", $"Bearer {token.Id}");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ConnectWithoutAuth_Returns401()
    {
        using var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v2/push");
        request.Headers.Add("Accept", "text/event-stream");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        using var response = await client.SendAsync(request, cts.Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConnectWithInvalidToken_Returns401()
    {
        using var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v2/push");
        request.Headers.Add("Accept", "text/event-stream");
        request.Headers.Add("Authorization", "Bearer invalid-token-xyz");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        using var response = await client.SendAsync(request, cts.Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConnectWithAccessTokenQueryParam_Succeeds()
    {
        var token = await CreateTokenAsync();

        using var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v2/push?access_token={token.Id}");
        request.Headers.Add("Accept", "text/event-stream");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ConnectedClient_ReceivesEntityChangedMessage()
    {
        var token = await CreateTokenAsync();
        var orgId = token.OrganizationId;

        using var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v2/push");
        request.Headers.Add("Accept", "text/event-stream");
        request.Headers.Add("Authorization", $"Bearer {token.Id}");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        // Read the initial "Connected" event
        string? connectedEvent = await ReadSseEventAsync(reader, cts.Token);
        Assert.NotNull(connectedEvent);
        Assert.Contains("Connected", connectedEvent);

        // Publish an EntityChanged message to the organization
        var entityChanged = new EntityChanged
        {
            Id = "stack-123",
            Type = "Stack",
            ChangeType = ChangeType.Saved
        };
        entityChanged.Data[ExtendedEntityChanged.KnownKeys.OrganizationId] = orgId;
#pragma warning disable xUnit1051
        await _messagePublisher.PublishAsync(entityChanged);
#pragma warning restore xUnit1051

        // Wait for and read the message
        string? receivedEvent = await ReadSseEventAsync(reader, cts.Token);
        Assert.NotNull(receivedEvent);
        Assert.Contains("StackChanged", receivedEvent);
        Assert.Contains("stack-123", receivedEvent);
    }

    [Fact]
    public async Task SseEndpoint_IsExemptFromThrottling()
    {
        var token = await CreateTokenAsync();

        using var client = _server.CreateClient();

        // Make multiple SSE connection attempts - should not be throttled
        for (int i = 0; i < 5; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v2/push");
            request.Headers.Add("Accept", "text/event-stream");
            request.Headers.Add("Authorization", $"Bearer {token.Id}");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            // Should never be 429
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    /// <summary>
    /// Read a single SSE event (terminated by double newline) from the stream.
    /// </summary>
    private static async Task<string?> ReadSseEventAsync(StreamReader reader, CancellationToken ct)
    {
        var sb = new StringBuilder();
        int emptyLineCount = 0;

        while (!ct.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(ct);
            if (line is null)
                return sb.Length > 0 ? sb.ToString() : null;

            if (line.Length == 0)
            {
                emptyLineCount++;
                if (emptyLineCount >= 1 && sb.Length > 0)
                    return sb.ToString();
                continue;
            }

            // Skip comments (keep-alive)
            if (line.StartsWith(':'))
                continue;

            emptyLineCount = 0;
            sb.AppendLine(line);
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    private async Task<Token> CreateTokenAsync()
    {
        var tokenData = GetService<TokenData>();
        return tokenData.GenerateSampleUserToken();
    }
}
