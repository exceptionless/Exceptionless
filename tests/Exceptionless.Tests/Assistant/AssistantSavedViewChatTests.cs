using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Foundatio.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Exceptionless.Tests.Assistant;

/// <summary>Runs Exie's chat endpoint against a scripted model, so saved view tools execute as a signed-in user in the app.</summary>
public sealed class AssistantSavedViewChatTests : IntegrationTestsBase
{
    private static readonly ScriptedProviderHandler s_provider = new();

    public AssistantSavedViewChatTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);
        services.ReplaceSingleton<IHttpClientFactory>(_ => new ScriptedHttpClientFactory(s_provider));
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
        var options = GetService<AppOptions>().AssistantOptions;
        options.Enabled = true;
        options.ApiKey = "test-key";
    }

    [Fact]
    public async Task Chat_CreateWithoutProjects_UsesTheCurrentProjectAndStaysPrivate()
    {
        // Arrange
        s_provider.Script(ToolCall("create_saved_view", new { name = "Production errors", viewType = "stacks", projectIds = Array.Empty<string>(), environments = new[] { "production" }, shared = false }));

        // Act
        string[] events = await SendChatAsync("Create a view of production errors.", SampleDataService.TEST_PROJECT_ID);

        // Assert
        string toolResult = Assert.Single(events, item => item.Contains("\"tool_result\"", StringComparison.Ordinal));
        Assert.Contains("/stack/production-errors", toolResult, StringComparison.Ordinal);
        await RefreshDataAsync();
        var views = await GetService<ISavedViewRepository>().GetByOrganizationIdAsync(SampleDataService.TEST_ORG_ID, o => o.PageLimit(100));
        var view = Assert.Single(views.Documents, item => item.Name == "Production errors");
        Assert.NotNull(view.UserId);
        Assert.StartsWith($"project:{SampleDataService.TEST_PROJECT_ID} environment:production", view.Filter, StringComparison.Ordinal);
    }

    private async Task<string[]> SendChatAsync(string prompt, string projectId)
    {
        using var client = CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "assistant/chat");
        string credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{SampleDataService.TEST_ORG_USER_EMAIL}:{SampleDataService.TEST_ORG_USER_PASSWORD}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = JsonContent.Create(new
        {
            conversation_id = Guid.NewGuid().ToString("N"),
            messages = new[] { new { role = "user", content = prompt } },
            organization_id = SampleDataService.TEST_ORG_ID,
            project_id = projectId,
            path = $"/project/{projectId}/stacks"
        });

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ToolCall(string name, object arguments)
    {
        string payload = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { delta = new { tool_calls = new[] { new { index = 0, id = "call-1", function = new { name, arguments = JsonSerializer.Serialize(arguments) } } } } }
            }
        });

        return $"data: {payload}\n\ndata: [DONE]\n";
    }

    private sealed class ScriptedHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class ScriptedProviderHandler : HttpMessageHandler
    {
        private const string FinalAnswer = "data: {\"choices\":[{\"delta\":{\"content\":\"Done.\"}}]}\n\ndata: [DONE]\n";
        private readonly Queue<string> _responses = new();

        public void Script(params string[] responses)
        {
            _responses.Clear();
            foreach (string response in responses)
                _responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string content = _responses.TryDequeue(out string? response) ? response : FinalAnswer;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "text/event-stream")
            });
        }
    }
}
