using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Serialization;
using Exceptionless.Core.Utility;
using Exceptionless.Web.Assistant;
using Xunit;

namespace Exceptionless.Tests.Assistant;

/// <summary>
/// Opt-in, provider-backed quality checks. These exercise the real HTTP endpoint, model,
/// authentication, Elasticsearch data, and MCP tools. They intentionally do not run in the
/// normal test suite because each run makes billable provider requests.
/// </summary>
public sealed class AssistantQualityEvaluationTests : IntegrationTestsBase
{
    private static readonly JsonSerializerOptions s_jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureExceptionlessApiDefaults();
    private PersistentEvent _currentEvent = null!;
    private Stack _currentStack = null!;

    public AssistantQualityEvaluationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    public static bool EvaluationsEnabled
        => String.Equals(Environment.GetEnvironmentVariable("RUN_ASSISTANT_EVALS"), "true", StringComparison.OrdinalIgnoreCase);

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
        var (stacks, events) = await CreateDataAsync(data =>
        {
            data.Event()
                .TestProject()
                .Type(Event.KnownTypes.Error)
                .Date(TimeProvider.GetUtcNow())
                .Message("Assistant evaluation database timeout");
            data.Event()
                .TestProject()
                .Type(Event.KnownTypes.Error)
                .Date(TimeProvider.GetUtcNow().AddMinutes(-5))
                .Message("Assistant evaluation null reference");
        });

        _currentEvent = events[0];
        _currentStack = stacks.Single(stack => stack.Id == _currentEvent.StackId);
    }

    [Fact(Skip = "Set RUN_ASSISTANT_EVALS=true to run the billable assistant quality gate.", SkipUnless = nameof(EvaluationsEnabled))]
    [Trait("Category", "AssistantEvaluation")]
    public async Task CurrentPage_UsesTheCurrentEventOnly()
    {
        RequireEvaluationConfiguration();

        var currentPage = await SendAssistantTurnAsync(
            "What am I looking at, and what is the most useful thing to investigate next?",
            $"/stack/{_currentStack.Id}/event/{_currentEvent.Id}",
            SampleDataService.TEST_PROJECT_ID);
        AssertSuccessfulAnswer(currentPage);
        Assert.Equal(1, currentPage.ToolCalls.Count(call => call == "get_event"));
        Assert.DoesNotContain("get_stack", currentPage.ToolCalls);
        Assert.DoesNotContain("list_projects", currentPage.ToolCalls);
        Assert.DoesNotContain("search_stacks", currentPage.ToolCalls);
    }

    [Fact(Skip = "Set RUN_ASSISTANT_EVALS=true to run the billable assistant quality gate.", SkipUnless = nameof(EvaluationsEnabled))]
    [Trait("Category", "AssistantEvaluation")]
    public async Task ProjectTopErrors_SearchesTheCurrentProjectOnce()
    {
        RequireEvaluationConfiguration();

        var projectTopErrors = await SendAssistantTurnAsync(
            "What are the top errors in this project in the last 24 hours? Link each result.",
            "/stack",
            SampleDataService.TEST_PROJECT_ID);
        AssertSuccessfulAnswer(projectTopErrors);
        Assert.Equal(1, projectTopErrors.ToolCalls.Count(call => call == "search_stacks"));
        Assert.DoesNotContain("list_projects", projectTopErrors.ToolCalls);
        Assert.Contains("/stack/", projectTopErrors.Text, StringComparison.Ordinal);
    }

    [Fact(Skip = "Set RUN_ASSISTANT_EVALS=true to run the billable assistant quality gate.", SkipUnless = nameof(EvaluationsEnabled))]
    [Trait("Category", "AssistantEvaluation")]
    public async Task OrganizationTopErrors_SearchesEachProjectWithoutToolErrors()
    {
        RequireEvaluationConfiguration();

        // This is the "Top errors today" starter prompt shown on organization-wide pages.
        var organizationTopErrors = await SendAssistantTurnAsync(
            "What are my top errors in the last 24 hours?",
            "/stack/all",
            projectId: null);
        AssertSuccessfulAnswer(organizationTopErrors);
        Assert.Equal(1, organizationTopErrors.ToolCalls.Count(call => call == "list_projects"));
        Assert.InRange(organizationTopErrors.ToolCalls.Count(call => call == "search_stacks"), 1, AssistantLimits.MaximumProjectsPerTurn);
        Assert.Contains("/stack/", organizationTopErrors.Text, StringComparison.Ordinal);
    }

    [Fact(Skip = "Set RUN_ASSISTANT_EVALS=true to run the billable assistant quality gate.", SkipUnless = nameof(EvaluationsEnabled))]
    [Trait("Category", "AssistantEvaluation")]
    public async Task ClientSetup_UsesProjectSetupOnly()
    {
        RequireEvaluationConfiguration();

        var clientSetup = await SendAssistantTurnAsync(
            "How do I configure this project to start sending events?",
            AssistantRoutes.ProjectStacks(SampleDataService.TEST_PROJECT_ID),
            SampleDataService.TEST_PROJECT_ID);
        AssertSuccessfulAnswer(clientSetup);
        Assert.Equal(["get_project_setup"], clientSetup.ToolCalls);
        Assert.Contains(AssistantRoutes.ProjectConfigure(SampleDataService.TEST_PROJECT_ID), clientSetup.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("exceptionless (pip)", clientSetup.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exceptionless (gem)", clientSetup.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exceptionless (Composer)", clientSetup.Text, StringComparison.OrdinalIgnoreCase);
    }

    private void RequireEvaluationConfiguration()
    {
        if (!GetService<AppOptions>().AssistantOptions.IsConfigured)
            Assert.Skip("Set EX_Assistant__ApiKey before running the assistant quality gate.");
    }

    private async Task<EvaluationTurn> SendAssistantTurnAsync(string prompt, string path, string? projectId)
    {
        using var client = CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "assistant/chat");
        // Exie needs a signed-in user, so authenticate as an organization member rather than with an access token.
        string credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{SampleDataService.TEST_ORG_USER_EMAIL}:{SampleDataService.TEST_ORG_USER_PASSWORD}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = JsonContent.Create(new
        {
            conversation_id = Guid.NewGuid().ToString("N"),
            messages = new[] { new { role = "user", content = prompt } },
            organization_id = SampleDataService.TEST_ORG_ID,
            project_id = projectId,
            path
        });

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stream);
        var events = new List<AssistantStreamEvent>();
        while (await reader.ReadLineAsync(TestContext.Current.CancellationToken) is { } line)
        {
            if (String.IsNullOrWhiteSpace(line))
                continue;

            var item = JsonSerializer.Deserialize<AssistantStreamEvent>(line, s_jsonOptions);
            if (item is not null)
                events.Add(item);
        }

        return new EvaluationTurn(
            String.Concat(events.Where(item => item.Type == "text_delta").Select(item => item.Text)),
            events.Where(item => item.Type == "tool_call" && item.ToolName is not null).Select(item => item.ToolName!).ToArray(),
            events);
    }

    private static void AssertSuccessfulAnswer(EvaluationTurn turn)
    {
        Assert.DoesNotContain(turn.Events, item => item.Type == "error");
        // A turn that recovers from a malformed tool call still wastes the tool budget and shows the user failed steps.
        Assert.DoesNotContain(turn.Events, item => item.Type == "tool_result" && IsFailedToolResult(item.Result));
        Assert.Contains(turn.Events, item => item.Type == "done");
        Assert.False(String.IsNullOrWhiteSpace(turn.Text));
        Assert.NotEmpty(turn.ToolCalls);
        Assert.DoesNotContain("DSML", turn.Text, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFailedToolResult(string? result)
    {
        if (String.IsNullOrWhiteSpace(result))
            return false;

        try
        {
            using var document = JsonDocument.Parse(result);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("ok", out var ok)
                && ok.ValueKind == JsonValueKind.False;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record EvaluationTurn(
        string Text,
        IReadOnlyCollection<string> ToolCalls,
        IReadOnlyCollection<AssistantStreamEvent> Events);
}
