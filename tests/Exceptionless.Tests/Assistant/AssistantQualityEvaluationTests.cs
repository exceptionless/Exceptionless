using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Repositories;
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
    private PersistentEvent _checkoutEvent = null!;
    private Stack _checkoutStack = null!;

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
            data.Event()
                .TestProject()
                .Type(Event.KnownTypes.Error)
                .Date(TimeProvider.GetUtcNow().AddMinutes(-10))
                .Source("Shop.Checkout.OrderService")
                .RequestInfo(new RequestInfo { HttpMethod = "POST", Host = "shop.example.com", Path = "/checkout/submit", IsSecure = true })
                .Version("4.2.0")
                .Mutate(ev => ev.SetError(new Error
                {
                    Type = "System.NullReferenceException",
                    Message = "Object reference not set to an instance of an object.",
                    StackTrace =
                    [
                        new StackFrame
                        {
                            DeclaringNamespace = "Shop.Checkout",
                            DeclaringType = "OrderService",
                            Name = "CalculateTotal",
                            FileName = "src/Shop/Checkout/OrderService.cs",
                            LineNumber = 87,
                            Parameters = [new Parameter { Type = "Order", Name = "order" }]
                        },
                        new StackFrame
                        {
                            DeclaringNamespace = "Shop.Checkout",
                            DeclaringType = "CheckoutController",
                            Name = "Submit",
                            FileName = "src/Shop/Checkout/CheckoutController.cs",
                            LineNumber = 42
                        }
                    ],
                    Data = new DataDictionary { ["DiscountCode"] = "SPRING25", ["Order.ShippingAddress"] = "null" }
                }));
        });

        _currentEvent = events[0];
        _currentStack = stacks.Single(stack => stack.Id == _currentEvent.StackId);
        _checkoutEvent = events[2];
        _checkoutStack = stacks.Single(stack => stack.Id == _checkoutEvent.StackId);
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
        // Periods go to the server as date math instead of timestamps the model computed.
        Assert.All(GetToolArguments(projectTopErrors, "search_stacks", "time"), time => Assert.Matches(@"^\[now-(24h|1d) TO now\]$", time));
    }

    [Fact(Skip = "Set RUN_ASSISTANT_EVALS=true to run the billable assistant quality gate.", SkipUnless = nameof(EvaluationsEnabled))]
    [Trait("Category", "AssistantEvaluation")]
    public async Task ProjectErrorsToday_SearchesTheCalendarDay()
    {
        RequireEvaluationConfiguration();

        var today = await SendAssistantTurnAsync(
            "Which errors happened in this project today?",
            "/stack",
            SampleDataService.TEST_PROJECT_ID,
            timeZone: "America/Chicago");
        AssertSuccessfulAnswer(today);
        Assert.Equal(1, today.ToolCalls.Count(call => call == "search_stacks"));
        Assert.Equal(["[now/d TO now/d]"], GetToolArguments(today, "search_stacks", "time"));
    }

    [Fact(Skip = "Set RUN_ASSISTANT_EVALS=true to run the billable assistant quality gate.", SkipUnless = nameof(EvaluationsEnabled))]
    [Trait("Category", "AssistantEvaluation")]
    public async Task OrganizationTopErrors_SearchesEachProjectWithoutToolErrors()
    {
        RequireEvaluationConfiguration();

        // This is the "Top errors in the last 24 hours" starter prompt shown on organization-wide pages.
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

    [Fact(Skip = "Set RUN_ASSISTANT_EVALS=true to run the billable assistant quality gate.", SkipUnless = nameof(EvaluationsEnabled))]
    [Trait("Category", "AssistantEvaluation")]
    public async Task ErrorPage_ExplainsCauseFromStackTrace()
    {
        RequireEvaluationConfiguration();

        var explanation = await SendAssistantTurnAsync(
            "Why is this happening and how do I fix it?",
            $"/stack/{_checkoutStack.Id}/event/{_checkoutEvent.Id}",
            SampleDataService.TEST_PROJECT_ID);
        AssertSuccessfulAnswer(explanation);
        Assert.Equal(1, explanation.ToolCalls.Count(call => call == "get_event"));
        Assert.DoesNotContain("search_stacks", explanation.ToolCalls);
        // The answer should be grounded in the captured frame, not a generic null reference explanation.
        Assert.Contains("CalculateTotal", explanation.Text, StringComparison.Ordinal);
    }

    [Fact(Skip = "Set RUN_ASSISTANT_EVALS=true to run the billable assistant quality gate.", SkipUnless = nameof(EvaluationsEnabled))]
    [Trait("Category", "AssistantEvaluation")]
    public async Task StackPage_MarksTheCurrentStackFixed()
    {
        RequireEvaluationConfiguration();

        var markFixed = await SendAssistantTurnAsync(
            "Mark this as fixed in 4.2.1.",
            $"/stack/{_checkoutStack.Id}",
            SampleDataService.TEST_PROJECT_ID);
        AssertSuccessfulAnswer(markFixed);
        Assert.Equal(1, markFixed.ToolCalls.Count(call => call == "update_stack_status"));
        Assert.DoesNotContain("search_stacks", markFixed.ToolCalls);

        var stack = await GetService<IStackRepository>().GetByIdAsync(_checkoutStack.Id);
        Assert.NotNull(stack);
        Assert.Equal(StackStatus.Fixed, stack.Status);
        Assert.Equal("4.2.1", stack.FixedInVersion);
    }

    private void RequireEvaluationConfiguration()
    {
        if (!GetService<AppOptions>().AssistantOptions.IsConfigured)
            Assert.Skip("Set EX_Assistant__ApiKey before running the assistant quality gate.");
    }

    private async Task<EvaluationTurn> SendAssistantTurnAsync(string prompt, string path, string? projectId, string? timeZone = null, [CallerMemberName] string scenario = "")
    {
        var usageService = GetService<AssistantUsageService>();
        long costBefore = (await usageService.GetMonthlyUsageAsync(SampleDataService.TEST_ORG_ID)).CostInMicrodollars;
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        double? firstTextMilliseconds = null;
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
            path,
            time_zone = timeZone
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
            if (item is null)
                continue;

            if (item.Type == "text_delta")
                firstTextMilliseconds ??= System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            events.Add(item);
        }

        var turn = new EvaluationTurn(
            String.Concat(events.Where(item => item.Type == "text_delta").Select(item => item.Text)),
            events.Where(item => item.Type == "tool_call" && item.ToolName is not null).Select(item => item.ToolName!).ToArray(),
            events);
        long costAfter = (await usageService.GetMonthlyUsageAsync(SampleDataService.TEST_ORG_ID)).CostInMicrodollars;
        await RecordEvaluationTurnAsync(scenario, prompt, turn, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds, firstTextMilliseconds, costAfter - costBefore);
        return turn;
    }

    /// <summary>Appends each turn to ASSISTANT_EVAL_RESULTS (JSON lines) so model runs can be compared side by side.</summary>
    private async Task RecordEvaluationTurnAsync(string scenario, string prompt, EvaluationTurn turn, double durationMilliseconds, double? firstTextMilliseconds, long costInMicrodollars)
    {
        string? resultsPath = Environment.GetEnvironmentVariable("ASSISTANT_EVAL_RESULTS");
        if (String.IsNullOrWhiteSpace(resultsPath))
            return;

        string line = JsonSerializer.Serialize(new
        {
            scenario,
            model = GetService<AppOptions>().AssistantOptions.Model,
            prompt,
            text = turn.Text,
            tool_calls = turn.Events.Where(item => item.Type == "tool_call").Select(item => new { name = item.ToolName, arguments = item.Arguments }),
            failed_tool_results = turn.Events.Count(item => item.Type == "tool_result" && IsFailedToolResult(item.Result)),
            errors = turn.Events.Where(item => item.Type == "error").Select(item => item.Message),
            duration_ms = Math.Round(durationMilliseconds),
            first_text_ms = firstTextMilliseconds is null ? (double?)null : Math.Round(firstTextMilliseconds.Value),
            cost_usd = costInMicrodollars / 1_000_000m
        }, s_jsonOptions);
        await File.AppendAllTextAsync(resultsPath, line + Environment.NewLine, TestContext.Current.CancellationToken);
    }

    private static void AssertSuccessfulAnswer(EvaluationTurn turn)
    {
        Assert.DoesNotContain(turn.Events, item => item.Type == "error");
        // A turn that recovers from a malformed tool call still wastes the tool budget and shows the user failed steps.
        Assert.DoesNotContain(turn.Events, item => item.Type == "tool_result" && IsFailedToolResult(item.Result));
        Assert.Contains(turn.Events, item => item.Type == "done");
        Assert.False(String.IsNullOrWhiteSpace(turn.Text));
        Assert.NotEmpty(turn.ToolCalls);
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

    private static string[] GetToolArguments(EvaluationTurn turn, string toolName, string argumentName)
    {
        return turn.Events
            .Where(item => item.Type == "tool_call" && item.ToolName == toolName && item.Arguments is not null)
            .Select(item => JsonDocument.Parse(item.Arguments!).RootElement)
            .Where(arguments => arguments.TryGetProperty(argumentName, out var value) && value.ValueKind == JsonValueKind.String)
            .Select(arguments => arguments.GetProperty(argumentName).GetString()!)
            .ToArray();
    }

    private sealed record EvaluationTurn(
        string Text,
        IReadOnlyCollection<string> ToolCalls,
        IReadOnlyCollection<AssistantStreamEvent> Events);
}
