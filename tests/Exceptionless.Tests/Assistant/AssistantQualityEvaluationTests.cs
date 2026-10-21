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
using Exceptionless.Tests.Utility;
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
            // A second, older occurrence of the same stack, so "today" (1) and all-time (2) totals differ.
            var checkout = AddCheckoutEvent(data, TimeProvider.GetUtcNow().AddMinutes(-10));
            AddCheckoutEvent(data, TimeProvider.GetUtcNow().AddDays(-3)).Stack(checkout);
        });

        _currentEvent = events[0];
        _currentStack = stacks.Single(stack => stack.Id == _currentEvent.StackId);
        _checkoutEvent = events[2];
        _checkoutStack = stacks.Single(stack => stack.Id == _checkoutEvent.StackId);
    }

    private static EventDataBuilder AddCheckoutEvent(DataBuilder data, DateTimeOffset date)
    {
        return data.Event()
            .TestProject()
            .Type(Event.KnownTypes.Error)
            .Date(date)
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
        // Ranking a period needs per-period counts, which count_events gives in one call.
        Assert.Equal(["count_events"], projectTopErrors.ToolCalls);
        Assert.Equal(["stack"], GetToolArguments(projectTopErrors, "count_events", "groupBy"));
        Assert.Contains("/stack/", projectTopErrors.Text, StringComparison.Ordinal);
        // Periods go to the server as date math instead of timestamps the model computed.
        Assert.All(GetToolArguments(projectTopErrors, "count_events", "time"), time => Assert.Matches(@"^\[now-(24h|1d) TO now\]$", time));
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
        Assert.Single(today.ToolCalls, call => call is "search_stacks" or "count_events");
        Assert.Equal(["[now/d TO now/d]"], [.. GetToolArguments(today, "search_stacks", "time"), .. GetToolArguments(today, "count_events", "time")]);
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
        Assert.InRange(organizationTopErrors.ToolCalls.Count(call => call == "count_events"), 1, AssistantLimits.MaximumProjectsPerTurn);
        Assert.Equal(organizationTopErrors.ToolCalls.Count - 1, organizationTopErrors.ToolCalls.Count(call => call == "count_events"));
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

    /// <summary>
    /// A way of asking about a period, the date math Exie must send for it, and the tools it may use. Patterns
    /// allow equivalent spellings such as [now-24h TO now] and [now-1d TO now]. A null pattern means no time limit.
    /// </summary>
    private sealed record TimeRangeScenario(string Prompt, string Page, string Tool, string? TimePattern, string? FollowUpTo = null);

    private const string AnyOrNoTime = "(any)";

    /// <summary>Weeks start on Sunday, so "since Monday" must name the most recent Monday in the user's time zone.</summary>
    private static string SinceMondayPattern
    {
        get
        {
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Chicago")).DateTime);
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            return $@"^\[{monday:yyyy-MM-dd} TO now\]$";
        }
    }
    // Listing what happened in a period may use either tool; ranking the top errors must use count_events.
    private const string Either = "search_stacks|count_events";

    private static readonly Dictionary<string, TimeRangeScenario> s_timeRangeScenarios = new()
    {
        ["last_24_hours"] = new("What are the top errors in this project in the last 24 hours?", "project", "count_events", @"^\[now-(24h|1d) TO now\]$"),
        ["last_hour"] = new("Were there any new errors in this project in the last hour?", "project", Either, @"^\[now-(1h|60m) TO now\]$"),
        ["last_2_days"] = new("Which errors did this project have over the last 2 days?", "project", Either, @"^\[now-(2d|48h)(/d)? TO now\]$"),
        ["past_7_days"] = new("What errors happened in this project in the past 7 days?", "project", Either, @"^\[now-(7d|1w)(/d)? TO now\]$"),
        ["last_30_days"] = new("What are the top errors in this project over the last 30 days?", "project", "count_events", @"^\[now-30d(/d)? TO now\]$"),
        ["today"] = new("Which errors happened in this project today?", "project", Either, @"^\[now/d TO now(/d)?\]$"),
        ["yesterday"] = new("What errors did this project have yesterday?", "project", Either, @"^\[now-1d/d TO now-1d/d\]$"),
        ["this_week"] = new("Show me this week's top errors in this project.", "project", "count_events", @"^\[now/w TO now(/w)?\]$"),
        ["last_week"] = new("What were the top errors in this project last week?", "project", "count_events", @"^\[now-1w/w TO now-1w/w\]$"),
        ["this_month"] = new("What are the top errors in this project this month?", "project", "count_events", @"^\[now/M TO now(/M)?\]$"),
        ["since_monday"] = new("What errors has this project had since Monday?", "project", Either, SinceMondayPattern),
        ["explicit_day"] = new("What errors happened in this project on October 1, 2026?", "project", Either, @"^\[2026-10-01 TO 2026-10-01\]$"),
        ["explicit_range"] = new("Show the top errors in this project between October 1 and October 15, 2026.", "project", "count_events", @"^\[2026-10-01 TO 2026-10-15\]$"),
        ["no_time"] = new("What are the top errors in this project?", "project", "count_events", @"^\[now-7d TO now\]$"),
        ["stack_today"] = new("How many times did this stack occur today?", "stack", "get_stack_events", @"^\[now/d TO now(/d)?\]$"),
        ["stack_all_time"] = new("How many times has this stack occurred in total?", "stack", "get_stack", AnyOrNoTime),
        ["organization_today"] = new("Which errors happened today across all of my projects?", "organization", Either, @"^\[now/d TO now(/d)?\]$"),
        ["follow_up_yesterday"] = new("And yesterday?", "project", Either, @"^\[now-1d/d TO now-1d/d\]$", FollowUpTo: "today")
    };

    public static TheoryData<string, int> TimeRangeScenarios()
    {
        int runs = Int32.TryParse(Environment.GetEnvironmentVariable("ASSISTANT_EVAL_RUNS"), out int value) && value > 0 ? value : 1;
        // ASSISTANT_EVAL_SCENARIOS limits a run to some scenarios, for example stack_today,stack_all_time.
        string[] only = (Environment.GetEnvironmentVariable("ASSISTANT_EVAL_SCENARIOS") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var data = new TheoryData<string, int>();
        foreach (string name in s_timeRangeScenarios.Keys.Where(name => only.Length == 0 || only.Contains(name)))
        {
            for (int run = 1; run <= runs; run++)
                data.Add(name, run);
        }

        return data;
    }

    [Theory(Skip = "Set RUN_ASSISTANT_EVALS=true to run the billable assistant quality gate.", SkipUnless = nameof(EvaluationsEnabled))]
    [MemberData(nameof(TimeRangeScenarios))]
    [Trait("Category", "AssistantEvaluation")]
    public async Task TimeRange_SendsDateMathInOneSearch(string name, int run)
    {
        RequireEvaluationConfiguration();
        var scenario = s_timeRangeScenarios[name];
        (string path, string? projectId) = scenario.Page switch
        {
            "stack" => ($"/stack/{_checkoutStack.Id}", SampleDataService.TEST_PROJECT_ID),
            "organization" => ("/stack/all", (string?)null),
            _ => ("/stack", SampleDataService.TEST_PROJECT_ID)
        };

        List<(string Role, string Content)>? history = null;
        if (scenario.FollowUpTo is not null)
        {
            var first = s_timeRangeScenarios[scenario.FollowUpTo];
            var earlier = await SendAssistantTurnAsync(first.Prompt, path, projectId, "America/Chicago", scenario: $"time_{scenario.FollowUpTo}_for_{name}");
            AssertSuccessfulAnswer(earlier);
            history = [("user", first.Prompt), ("assistant", earlier.Text)];
        }

        var turn = await SendAssistantTurnAsync(scenario.Prompt, path, projectId, "America/Chicago", history, scenario: $"time_{name}_{run}");

        AssertSuccessfulAnswer(turn);
        string[] tools = scenario.Tool.Split('|');
        var times = turn.Events
            .Where(item => item.Type == "tool_call" && tools.Contains(item.ToolName))
            .Select(item => GetStringArgument(item.Arguments, "time"))
            .ToArray();
        Assert.NotEmpty(times);
        if (scenario.TimePattern != AnyOrNoTime)
            Assert.All(times, time => Assert.Matches(scenario.TimePattern!, time ?? "(no time)"));
        if (scenario.Tool == "count_events")
            Assert.All(GetToolArguments(turn, "count_events", "groupBy"), groupBy => Assert.Equal("stack", groupBy));

        // The checkout stack occurred once today and once three days ago, so a total of 2 means Exie ignored the period.
        if (name == "stack_today")
            Assert.DoesNotMatch(@"\b(2|two) (times|occurrences)\b", turn.Text);
        if (name == "stack_all_time")
            Assert.Matches(@"\b(2|two) (times|occurrences)\b", turn.Text);

        // One lookup answers a scoped question; more means Exie retried or searched again.
        if (scenario.Page == "organization")
        {
            Assert.Equal(1, turn.ToolCalls.Count(call => call == "list_projects"));
            Assert.InRange(times.Length, 1, AssistantLimits.MaximumProjectsPerTurn);
        }
        else
        {
            Assert.Single(times);
            Assert.DoesNotContain("list_projects", turn.ToolCalls);
            Assert.InRange(turn.ToolCalls.Count, 1, 2);
        }
    }

    private static string? GetStringArgument(string? arguments, string name)
    {
        if (String.IsNullOrWhiteSpace(arguments))
            return null;

        using var document = JsonDocument.Parse(arguments);
        return document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? System.Text.RegularExpressions.Regex.Replace(value.GetString()!.Trim(), @"\s+", " ")
            : null;
    }

    private async Task<EvaluationTurn> SendAssistantTurnAsync(string prompt, string path, string? projectId, string? timeZone = null, IReadOnlyList<(string Role, string Content)>? history = null, [CallerMemberName] string scenario = "")
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
            messages = (history ?? []).Select(message => new { role = message.Role, content = message.Content }).Append(new { role = "user", content = prompt }).ToArray(),
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
