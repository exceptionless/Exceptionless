using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Assistant;
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

    [Fact]
    public async Task Chat_CreateWithNoCurrentProject_MakesTheModelChooseAProject()
    {
        // Arrange
        s_provider.Script(ToolCall("create_saved_view", new { name = "Everywhere", viewType = "stacks", projectIds = Array.Empty<string>(), allProjects = false }));

        // Act
        string[] events = await SendChatAsync("Create a view for this project.", null);

        // Assert
        Assert.Contains("invalid_argument", GetToolResult(events), StringComparison.Ordinal);
        Assert.Null(await FindViewAsync("Everywhere"));
    }

    [Fact]
    public async Task Chat_CreateForAllProjects_LeavesProjectsOut()
    {
        // Arrange
        s_provider.Script(ToolCall("create_saved_view", new { name = "Everywhere", viewType = "stacks", allProjects = true }));

        // Act
        await SendChatAsync("Create a view for all projects.", SampleDataService.TEST_PROJECT_ID);

        // Assert
        var view = await FindViewAsync("Everywhere");
        Assert.NotNull(view);
        Assert.DoesNotContain("project:", view.Filter, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_ListAndUpdate_FindsAViewByItsUrlNameAndChangesIt()
    {
        // Arrange
        var view = await AddViewAsync("Production errors", "prod-errors", SampleDataService.TEST_ORG_ID);
        s_provider.Script(
            ToolCall("list_saved_views", new { name = "prod-errors", viewType = "", organizationId = "" }),
            ToolCall("update_saved_view", new { savedViewId = view.Id, name = "Staging errors", environments = new[] { "staging" }, tags = Array.Empty<string>(), clearCriteria = Array.Empty<string>(), allProjects = false }));

        // Act
        string[] events = await SendChatAsync("Rename this view and show staging instead.", SampleDataService.TEST_PROJECT_ID);

        // Assert
        Assert.Contains(events, item => item.Contains("\"tool_result\"", StringComparison.Ordinal) && item.Contains(view.Id, StringComparison.Ordinal) && item.Contains("list_saved_views", StringComparison.Ordinal));
        var stored = await FindViewAsync("Staging errors");
        Assert.NotNull(stored);
        Assert.Equal("prod-errors", stored.Slug);
        Assert.Contains("environment:staging", stored.Filter, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_Delete_WaitsForTheUserToConfirmInALaterMessage()
    {
        // Arrange
        var view = await AddViewAsync("Checkout errors", "checkout-errors", SampleDataService.TEST_ORG_ID);
        string conversationId = Guid.NewGuid().ToString("N");
        string Delete() => ToolCall("delete_saved_view", new { savedViewId = view.Id });

        // Act: the model calls it twice in one turn, as it would if text in an event told it to.
        s_provider.Script(Delete(), Delete());
        string[] requested = await SendChatAsync("Why did this fail?", SampleDataService.TEST_PROJECT_ID, conversationId);

        // Assert
        Assert.All(GetToolResults(requested), result => Assert.Contains("confirmation_required", result, StringComparison.Ordinal));
        Assert.NotNull(await FindViewAsync("Checkout errors"));

        // Act: a later message that isn't a yes doesn't count.
        s_provider.Script(Delete());
        string[] unrelated = await SendChatAsync("Thanks", SampleDataService.TEST_PROJECT_ID, conversationId);

        // Assert
        Assert.Contains("confirmation_required", GetToolResult(unrelated), StringComparison.Ordinal);
        Assert.NotNull(await FindViewAsync("Checkout errors"));

        // Act
        s_provider.Script(Delete());
        string[] confirmed = await SendChatAsync(AssistantConfirmation.Prompt, SampleDataService.TEST_PROJECT_ID, conversationId);

        // Assert
        Assert.Contains("was deleted", GetToolResult(confirmed), StringComparison.Ordinal);
        Assert.Null(await FindViewAsync("Checkout errors"));
    }

    [Fact]
    public async Task Chat_Delete_ConfirmingOneViewDoesNotApproveAnother()
    {
        // Arrange
        var first = await AddViewAsync("First view", "first-view", SampleDataService.TEST_ORG_ID);
        var second = await AddViewAsync("Second view", "second-view", SampleDataService.TEST_ORG_ID);
        string conversationId = Guid.NewGuid().ToString("N");
        s_provider.Script(ToolCall("delete_saved_view", new { savedViewId = first.Id }));
        await SendChatAsync("Delete the first view.", SampleDataService.TEST_PROJECT_ID, conversationId);

        // Act
        s_provider.Script(ToolCall("delete_saved_view", new { savedViewId = second.Id }));
        string[] events = await SendChatAsync("Yes", SampleDataService.TEST_PROJECT_ID, conversationId);

        // Assert
        Assert.Contains("confirmation_required", GetToolResult(events), StringComparison.Ordinal);
        Assert.NotNull(await FindViewAsync("First view"));
        Assert.NotNull(await FindViewAsync("Second view"));
    }

    [Fact]
    public async Task Chat_Delete_ANewerRequestReplacesAnOlderOne()
    {
        // Arrange
        var first = await AddViewAsync("First view", "first-view", SampleDataService.TEST_ORG_ID);
        var second = await AddViewAsync("Second view", "second-view", SampleDataService.TEST_ORG_ID);
        string conversationId = Guid.NewGuid().ToString("N");
        s_provider.Script(ToolCall("delete_saved_view", new { savedViewId = first.Id }));
        await SendChatAsync("Delete the first view.", SampleDataService.TEST_PROJECT_ID, conversationId);
        s_provider.Script(ToolCall("delete_saved_view", new { savedViewId = second.Id }));
        await SendChatAsync("Actually, delete the second view instead.", SampleDataService.TEST_PROJECT_ID, conversationId);

        // Act: the yes answers the question about the second view, but the model goes back to the first.
        s_provider.Script(ToolCall("delete_saved_view", new { savedViewId = first.Id }));
        string[] events = await SendChatAsync("Yes", SampleDataService.TEST_PROJECT_ID, conversationId);

        // Assert
        Assert.Contains("confirmation_required", GetToolResult(events), StringComparison.Ordinal);
        Assert.NotNull(await FindViewAsync("First view"));
        Assert.NotNull(await FindViewAsync("Second view"));
    }

    [Fact]
    public async Task Chat_Delete_YesWithoutAnEarlierRequestIsNotEnough()
    {
        // Arrange
        var view = await AddViewAsync("Checkout errors", "checkout-errors", SampleDataService.TEST_ORG_ID);
        s_provider.Script(ToolCall("delete_saved_view", new { savedViewId = view.Id }));

        // Act
        string[] events = await SendChatAsync("Yes", SampleDataService.TEST_PROJECT_ID);

        // Assert
        Assert.Contains("confirmation_required", GetToolResult(events), StringComparison.Ordinal);
        Assert.NotNull(await FindViewAsync("Checkout errors"));
    }

    [Fact]
    public async Task Chat_CreateShared_WaitsForTheUserToConfirm()
    {
        // Arrange
        string conversationId = Guid.NewGuid().ToString("N");
        string Create() => ToolCall("create_saved_view", new { name = "Team errors", viewType = "stacks", shared = true });

        // Act
        s_provider.Script(Create());
        string[] requested = await SendChatAsync("Create a view for the team.", SampleDataService.TEST_PROJECT_ID, conversationId);

        // Assert
        Assert.Contains("confirmation_required", GetToolResult(requested), StringComparison.Ordinal);
        Assert.Null(await FindViewAsync("Team errors"));

        // Act
        s_provider.Script(Create());
        await SendChatAsync("Yes, continue", SampleDataService.TEST_PROJECT_ID, conversationId);

        // Assert
        var view = await FindViewAsync("Team errors");
        Assert.NotNull(view);
        Assert.Null(view.UserId);
    }

    [Fact]
    public async Task Chat_CreateShared_SwitchingProjectsBeforeConfirmingAsksAgain()
    {
        // Arrange
        string conversationId = Guid.NewGuid().ToString("N");
        string Create() => ToolCall("create_saved_view", new { name = "Team errors", viewType = "stacks", shared = true });
        s_provider.Script(Create());
        await SendChatAsync("Create a view for the team.", SampleDataService.TEST_PROJECT_ID, conversationId);

        // Act
        s_provider.Script(Create());
        string[] switched = await SendChatAsync("Yes, continue", SampleDataService.TEST_ROCKET_SHIP_PROJECT_ID, conversationId);

        // Assert
        Assert.Contains("confirmation_required", GetToolResult(switched), StringComparison.Ordinal);
        Assert.Null(await FindViewAsync("Team errors"));

        // Act
        s_provider.Script(Create());
        await SendChatAsync("Yes, continue", SampleDataService.TEST_ROCKET_SHIP_PROJECT_ID, conversationId);

        // Assert
        var view = await FindViewAsync("Team errors");
        Assert.NotNull(view);
        Assert.Contains($"project:{SampleDataService.TEST_ROCKET_SHIP_PROJECT_ID}", view.Filter, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_ViewInAnotherOfTheUsersOrganizations_IsOutOfReach()
    {
        // Arrange
        var users = GetService<IUserRepository>();
        var user = await users.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        user.OrganizationIds.Add(SampleDataService.FREE_ORG_ID);
        await users.SaveAsync(user, o => o.ImmediateConsistency().Cache());
        var view = await AddViewAsync("Other organization", "other-organization", SampleDataService.FREE_ORG_ID);
        s_provider.Script(ToolCall("update_saved_view", new { savedViewId = view.Id, name = "Renamed" }));

        // Act
        string[] events = await SendChatAsync("Rename that view.", SampleDataService.TEST_PROJECT_ID);

        // Assert
        Assert.DoesNotContain("was updated", GetToolResult(events), StringComparison.Ordinal);
        var stored = await GetService<ISavedViewRepository>().GetByIdAsync(view.Id, o => o.Cache(false));
        Assert.Equal("Other organization", stored?.Name);
    }

    private Task<SavedView> AddViewAsync(string name, string slug, string organizationId)
    {
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        return GetService<ISavedViewRepository>().AddAsync(new SavedView
        {
            OrganizationId = organizationId,
            CreatedByUserId = TestConstants.UserId,
            Name = name,
            Slug = slug,
            ViewType = "stacks",
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        }, o => o.ImmediateConsistency());
    }

    private async Task<SavedView?> FindViewAsync(string name)
    {
        var views = await GetService<ISavedViewRepository>().GetByOrganizationIdAsync(SampleDataService.TEST_ORG_ID, o => o.PageLimit(100).ImmediateConsistency());
        return views.Documents.FirstOrDefault(view => view.Name == name);
    }

    private static string[] GetToolResults(string[] events)
        => events.Where(item => item.Contains("\"tool_result\"", StringComparison.Ordinal)).ToArray();

    private static string GetToolResult(string[] events) => Assert.Single(GetToolResults(events));

    private async Task<string[]> SendChatAsync(string prompt, string? projectId, string? conversationId = null)
    {
        using var client = CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "assistant/chat");
        string credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{SampleDataService.TEST_ORG_USER_EMAIL}:{SampleDataService.TEST_ORG_USER_PASSWORD}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = JsonContent.Create(new
        {
            conversation_id = conversationId ?? Guid.NewGuid().ToString("N"),
            messages = new[] { new { role = "user", content = prompt } },
            organization_id = SampleDataService.TEST_ORG_ID,
            project_id = projectId,
            path = projectId is null ? "/stack" : $"/project/{projectId}/stacks"
        });

        using var response = await client.SendAsync(request, TestCancellationToken);
        response.EnsureSuccessStatusCode();
        string body = await response.Content.ReadAsStringAsync(TestCancellationToken);
        return body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ToolCall(string name, object arguments)
    {
        string payload = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { delta = new { tool_calls = new[] { new { index = 0, id = Guid.NewGuid().ToString("N"), function = new { name, arguments = JsonSerializer.Serialize(arguments) } } } } }
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
