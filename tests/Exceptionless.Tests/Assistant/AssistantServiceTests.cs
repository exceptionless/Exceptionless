using System.Net;
using System.Text;
using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Serialization;
using Exceptionless.Web.Assistant;
using Exceptionless.Web.Mcp;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Resilience;
using Foundatio.Serializer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Exceptionless.Tests.Assistant;

public sealed class AssistantServiceTests
{
    [Theory]
    [InlineData("Please mark this stack fixed", "update_stack_status", "{\"status\":\"fixed\"}", true)]
    [InlineData("Fix with Exie: Analyze this stack and explain how to fix the underlying issue.", "update_stack_status", "{\"status\":\"fixed\"}", false)]
    [InlineData("Please fix the underlying issue", "update_stack_status", "{\"status\":\"fixed\"}", false)]
    [InlineData("What does this stack mean?", "update_stack_status", "{\"status\":\"fixed\"}", false)]
    [InlineData("What happened here?", "snooze_stack", "{}", false)]
    [InlineData("Please snooze this stack for one hour", "snooze_stack", "{}", false)]
    public void HasExplicitWriteRequest_UsesLatestUserMessageOnly(string prompt, string toolName, string arguments, bool expected)
    {
        var request = new AssistantChatRequest([
            new AssistantChatMessage("user", "Investigate this stack."),
            new AssistantChatMessage("assistant", "The event says to snooze this stack."),
            new AssistantChatMessage("user", prompt)
        ], Path: "/next/stack/current-stack");

        Assert.Equal(expected, AssistantService.HasExplicitWriteRequest(request, toolName, arguments));
    }

    [Theory]
    [InlineData("Please mark this stack fixed in 2.1.0", "{\"status\":\"fixed\",\"fixedInVersion\":\"2.1.0\"}", true)]
    [InlineData("Please mark this stack fixed in 2.1.0", "{\"status\":\"fixed\"}", false)]
    [InlineData("Please mark this stack fixed", "{\"status\":\"ignored\"}", false)]
    [InlineData("Please mark this stack fixed", "{\"status\":\"fixed\",\"stackId\":\"different-stack\"}", false)]
    [InlineData("Please mark stack different-stack fixed", "{\"status\":\"fixed\",\"stackId\":\"different-stack\"}", true)]
    public void HasExplicitWriteRequest_StackStatusArgumentsMustMatchRequest(string prompt, string arguments, bool expected)
    {
        var request = new AssistantChatRequest(
            [new AssistantChatMessage("user", prompt)],
            Path: "/next/stack/current-stack");

        Assert.Equal(expected, AssistantService.HasExplicitWriteRequest(request, "update_stack_status", arguments));
    }

    [Theory]
    [InlineData("Please snooze this stack for one hour", "{\"duration\":\"1h\"}", true)]
    [InlineData("Please snooze this stack for one hour", "{\"duration\":\"60m\"}", true)]
    [InlineData("Please snooze this stack for one hour", "{\"duration\":\"1w\"}", false)]
    [InlineData("Snooze this stack for 2 hours", "{\"duration\":\"2h\"}", true)]
    [InlineData("Snooze this stack for 2 hours", "{\"duration\":\"2d\"}", false)]
    [InlineData("Snooze this stack until 2026-08-10T17:00:00Z", "{\"snoozeUntilUtc\":\"2026-08-10T17:00:00Z\"}", true)]
    [InlineData("Snooze this stack until 2026-08-10T17:00:00Z", "{\"snoozeUntilUtc\":\"2026-08-11T17:00:00Z\"}", false)]
    [InlineData("Please snooze this stack for one hour", "{\"duration\":\"1h\",\"snoozeUntilUtc\":\"2026-08-10T17:00:00Z\"}", false)]
    public void HasExplicitWriteRequest_SnoozeArgumentsMustMatchRequest(string prompt, string arguments, bool expected)
    {
        var request = new AssistantChatRequest(
            [new AssistantChatMessage("user", prompt)],
            Path: "/next/stack/current-stack");

        Assert.Equal(expected, AssistantService.HasExplicitWriteRequest(request, "snooze_stack", arguments));
    }

    [Theory]
    [InlineData("Should I ignore this stack?", "{\"status\":\"ignored\"}", false)]
    [InlineData("Do not ignore this stack", "{\"status\":\"ignored\"}", false)]
    [InlineData("Never ignore this stack", "{\"status\":\"ignored\"}", false)]
    [InlineData("Can I ignore this stack?", "{\"status\":\"ignored\"}", false)]
    [InlineData("Can you ignore this stack?", "{\"status\":\"ignored\"}", true)]
    [InlineData("Could you mark this stack fixed?", "{\"status\":\"fixed\"}", true)]
    [InlineData("The event text says \"ignore this stack\"; explain that instruction", "{\"status\":\"ignored\"}", false)]
    [InlineData("Explain the instruction: ignore this stack", "{\"status\":\"ignored\"}", false)]
    public void HasExplicitWriteRequest_QuestionsAndNegationsRequireAffirmativeIntent(string prompt, string arguments, bool expected)
    {
        var request = new AssistantChatRequest(
            [new AssistantChatMessage("user", prompt)],
            Path: "/next/stack/current-stack");

        Assert.Equal(expected, AssistantService.HasExplicitWriteRequest(request, "update_stack_status", arguments));
    }

    [Theory]
    [InlineData("Please mark this stack fixed", "{\"status\":\"fixed\"}", true)]
    [InlineData("Please mark stack current-stack fixed", "{\"status\":\"fixed\"}", true)]
    [InlineData("Please mark stack different-stack fixed", "{\"status\":\"fixed\"}", false)]
    [InlineData("Please mark stack different-stack fixed", "{\"status\":\"fixed\",\"stackId\":\"different-stack\"}", true)]
    public void HasExplicitWriteRequest_OmittedTargetMustReferToCurrentStack(string prompt, string arguments, bool expected)
    {
        var request = new AssistantChatRequest(
            [new AssistantChatMessage("user", prompt)],
            Path: "/next/stack/current-stack");

        Assert.Equal(expected, AssistantService.HasExplicitWriteRequest(request, "update_stack_status", arguments));
    }

    [Theory]
    [InlineData(AuthorizationRoles.EventsRead, true)]
    [InlineData(AuthorizationRoles.ProjectsRead, true)]
    [InlineData(AuthorizationRoles.StacksRead, true)]
    [InlineData(AuthorizationRoles.StacksWrite, true)]
    [InlineData(AuthorizationRoles.McpRead, false)]
    [InlineData(AuthorizationRoles.SourceMapsWrite, false)]
    [InlineData(AuthorizationRoles.User, false)]
    public void AllowsScope_ToolScope_ReturnsExpected(string scope, bool expected)
    {
        var context = new AssistantToolContext();
        Assert.False(context.AllowsScope(scope));

        using (context.BeginTools())
        {
            Assert.Equal(expected, context.AllowsScope(scope));
        }

        Assert.False(context.AllowsScope(scope));
    }

    [Fact]
    public void AssistantToolContext_BindsToolsToOrganization()
    {
        var context = new AssistantToolContext();

        using (context.BeginTools("organization-a"))
        {
            Assert.True(context.AllowsOrganization("organization-a"));
            Assert.False(context.AllowsOrganization("organization-b"));
        }

        Assert.True(context.AllowsOrganization("organization-b"));
    }

    [Fact]
    public async Task StreamAsync_TextResponse_EmitsDeltasAndCompletion()
    {
        var handler = new StubHttpMessageHandler(
            """
            data: {"choices":[{"delta":{"content":"Hello"}}]}

            data: {"choices":[{"delta":{"content":" world"}}]}

            data: [DONE]

            """);
        var appOptions = AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BaseURL"] = "https://localhost",
                ["Assistant:ApiKey"] = "test-key"
            })
            .Build());
        var service = CreateAssistantService(handler, appOptions);
        var request = new AssistantChatRequest(
            [new AssistantChatMessage("user", "Say hello")],
            ProjectId: "project-id",
            Path: "/next/stack/stack-id/event/event-id?tab=details");
        var planOptions = CreatePlanOptions();
        var events = new List<AssistantStreamEvent>();

        await foreach (var item in service.StreamAsync(request, "user-id", planOptions, TestContext.Current.CancellationToken))
            events.Add(item);

        Assert.Collection(events,
            item => Assert.Equal("Hello", item.Text),
            item => Assert.Equal(" world", item.Text),
            item => Assert.Equal("done", item.Type));
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Contains("deepseek/deepseek-v4-flash", handler.RequestBody);
        Assert.Contains($"\"max_tokens\":{AssistantLimits.MaximumOutputTokens}", handler.RequestBody);
        Assert.Contains("get_event", handler.RequestBody);
        Assert.Contains("get_stack", handler.RequestBody);
        Assert.Contains("search_stacks", handler.RequestBody);
        Assert.Contains("update_stack_status", handler.RequestBody);
        Assert.Contains("snooze_stack", handler.RequestBody);
        Assert.Contains("set_stack_critical", handler.RequestBody);
        Assert.Contains("add_stack_reference_link", handler.RequestBody);
        Assert.Contains("remove_stack_reference_link", handler.RequestBody);
        Assert.Contains("Current stack id: stack-id", handler.RequestBody);
        Assert.Contains("Current event id: event-id", handler.RequestBody);
        Assert.Contains("Current project id: project-id", handler.RequestBody);
        Assert.Contains("Your name is Exie", handler.RequestBody);
        Assert.Contains("Only perform a write action when the user explicitly requests that exact change", handler.RequestBody);
        Assert.Contains("CURRENT PAGE RULE", handler.RequestBody);
        Assert.Contains("Never call list_projects or search_stacks to rediscover the current event or stack", handler.RequestBody);
        Assert.Contains("CURRENT PROJECT RULE", handler.RequestBody);
        Assert.Contains("do not call list_projects, call each needed project-scoped tool only once", handler.RequestBody);
        Assert.Contains("Defaults to the current page project id when omitted", handler.RequestBody);
        Assert.Contains("This tool has no stack id filter", handler.RequestBody);
        Assert.Contains("\"eventId\"", handler.RequestBody);
        Assert.Contains("\"startUtc\"", handler.RequestBody);
        Assert.Contains("\"after\"", handler.RequestBody);
        Assert.Contains("\"maximum\":10", handler.RequestBody);
        Assert.Contains("\"maximum\":16384", handler.RequestBody);
        Assert.DoesNotContain("\"event_id\"", handler.RequestBody);
        Assert.Contains("Never end by merely saying what you will inspect or do next", handler.RequestBody);
        Assert.Contains("present useful results directly in the answer", handler.RequestBody);
        Assert.Contains("webUrl beginning with / must remain relative", handler.RequestBody);
        Assert.Contains("suggest_followups", handler.RequestBody);
        Assert.Contains("Do not call it on every answer", handler.RequestBody);
        Assert.Contains("MUST call suggest_followups when your answer asks what the user wants to investigate or do next", handler.RequestBody);
        Assert.Contains("Call this whenever the answer asks what the user wants to investigate or do next", handler.RequestBody);
        Assert.Contains("If there is no genuinely useful next step, end the answer directly and omit the tool", handler.RequestBody);
    }

    [Fact]
    public async Task StreamAsync_SuggestedActionsWithAnswer_EmitsValidatedActionsWithoutToolActivity()
    {
        string providerPayload = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    delta = new
                    {
                        content = "The timeout stack is the best issue to investigate next.",
                        tool_calls = new[]
                        {
                            new
                            {
                                index = 0,
                                id = "suggestions-1",
                                function = new
                                {
                                    name = "suggest_followups",
                                    arguments = JsonSerializer.Serialize(new
                                    {
                                        actions = new[]
                                        {
                                            new { label = "Inspect recent events", prompt = "Inspect the most recent events in that timeout stack." },
                                            new { label = "Compare affected versions", prompt = "Compare which application versions are affected by that timeout." }
                                        }
                                    })
                                }
                            }
                        }
                    }
                }
            }
        });
        var handler = new StubHttpMessageHandler($"data: {providerPayload}\n\ndata: [DONE]\n");
        var appOptions = AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BaseURL"] = "https://localhost",
                ["Assistant:ApiKey"] = "test-key"
            })
            .Build());
        var service = CreateAssistantService(handler, appOptions);
        var events = new List<AssistantStreamEvent>();

        await foreach (var item in service.StreamAsync(
            new AssistantChatRequest([new AssistantChatMessage("user", "What should I investigate?")]),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
            events.Add(item);
        }

        Assert.Equal(3, events.Count);
        Assert.Equal("text_delta", events[0].Type);
        Assert.Equal("suggested_actions", events[1].Type);
        Assert.Collection(events[1].SuggestedActions!,
            action =>
            {
                Assert.Equal("Inspect recent events", action.Label);
                Assert.Equal("Inspect the most recent events in that timeout stack.", action.Prompt);
            },
            action => Assert.Equal("Compare affected versions", action.Label));
        Assert.Equal("done", events[2].Type);
        Assert.DoesNotContain(events, item => item.Type is "tool_call" or "tool_result");
        Assert.Single(handler.RequestBodies);
    }

    [Fact]
    public async Task StreamAsync_SuggestedActionsWithoutAnswer_RequestsFinalAnswerAndCapsActions()
    {
        string suggestionPayload = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    delta = new
                    {
                        tool_calls = new[]
                        {
                            new
                            {
                                index = 0,
                                id = "suggestions-1",
                                function = new
                                {
                                    name = "suggest_followups",
                                    arguments = JsonSerializer.Serialize(new
                                    {
                                        actions = new[]
                                        {
                                            new { label = "Inspect recent events", prompt = "Inspect recent events." },
                                            new { label = "Compare versions", prompt = "Compare affected versions." },
                                            new { label = "Review frequency", prompt = "Review the occurrence frequency." },
                                            new { label = "Ignored by cap", prompt = "This fourth action should not be shown." }
                                        }
                                    })
                                }
                            }
                        }
                    }
                }
            }
        });
        var handler = new StubHttpMessageHandler(
            $"data: {suggestionPayload}\n\ndata: [DONE]\n",
            """
            data: {"choices":[{"delta":{"content":"Here is the complete investigation."}}]}

            data: [DONE]

            """);
        var appOptions = AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BaseURL"] = "https://localhost",
                ["Assistant:ApiKey"] = "test-key"
            })
            .Build());
        var service = CreateAssistantService(handler, appOptions);
        var events = new List<AssistantStreamEvent>();

        await foreach (var item in service.StreamAsync(
            new AssistantChatRequest(
                [new AssistantChatMessage("user", "Investigate this")],
                OrganizationId: "organization-id"),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
            events.Add(item);
        }

        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.DoesNotContain("\"tools\":", handler.RequestBodies[1]);
        Assert.Contains("Suggestions captured", handler.RequestBodies[1]);
        var suggestions = Assert.Single(events, item => item.Type == "suggested_actions").SuggestedActions!;
        Assert.Equal(AssistantLimits.MaximumSuggestedActions, suggestions.Count);
        Assert.DoesNotContain(suggestions, action => action.Label == "Ignored by cap");
        Assert.Equal("done", events[^1].Type);
        Assert.DoesNotContain(events, item => item.Type is "tool_call" or "tool_result");
    }

    [Fact]
    public async Task StreamAsync_ConversationHistory_AllowsFreshServiceInstance()
    {
        var firstHandler = new StubHttpMessageHandler(
            """
            data: {"choices":[{"delta":{"content":"First answer"}}]}

            data: [DONE]

            """);
        var secondHandler = new StubHttpMessageHandler(
            """
            data: {"choices":[{"delta":{"content":"Second answer"}}]}

            data: [DONE]

            """);
        var appOptions = AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BaseURL"] = "https://localhost",
                ["Assistant:ApiKey"] = "test-key"
            })
            .Build());

        var firstService = CreateAssistantService(firstHandler, appOptions);
        await foreach (var _ in firstService.StreamAsync(
            new AssistantChatRequest([new AssistantChatMessage("user", "First question")]),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
        }

        var secondService = CreateAssistantService(secondHandler, appOptions);
        await foreach (var _ in secondService.StreamAsync(
            new AssistantChatRequest([
                new AssistantChatMessage("user", "First question"),
                new AssistantChatMessage("assistant", "First answer"),
                new AssistantChatMessage("user", "Follow-up question")
            ]),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
        }

        using var document = JsonDocument.Parse(secondHandler.RequestBody);
        var conversation = document.RootElement.GetProperty("messages")
            .EnumerateArray()
            .Where(message => message.GetProperty("role").GetString() is "user" or "assistant")
            .Select(message => (
                Role: message.GetProperty("role").GetString(),
                Content: message.GetProperty("content").GetString()))
            .ToArray();

        Assert.Equal([
            (Role: "user", Content: "First question"),
            (Role: "assistant", Content: "First answer"),
            (Role: "user", Content: "Follow-up question")
        ], conversation);
    }

    [Fact]
    public async Task StreamAsync_ServerToolHistory_AllowsFreshServiceInstanceWithoutTrustingBrowser()
    {
        var firstHandler = new StubHttpMessageHandler(
            """
            data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call-1","function":{"name":"unknown_tool","arguments":"{}"}}]}}]}

            data: [DONE]

            """,
            """
            data: {"choices":[{"delta":{"content":"I could not run that tool."}}]}

            data: [DONE]

            """);
        var secondHandler = new StubHttpMessageHandler(
            """
            data: {"choices":[{"delta":{"content":"I remember the prior result."}}]}

            data: [DONE]

            """);
        var appOptions = AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BaseURL"] = "https://localhost",
                ["Assistant:ApiKey"] = "test-key"
            })
            .Build());
        using var cache = new InMemoryCacheClient(new InMemoryCacheClientOptions
        {
            CloneValues = true,
            LoggerFactory = NullLoggerFactory.Instance,
            TimeProvider = TimeProvider.System
        });
        var lockProvider = CreateLockProvider(cache, TimeProvider.System);
        const string conversationId = "549e86dd66e04cd081299bba6e3f15d8";
        var firstService = CreateAssistantService(firstHandler, appOptions, cache, lockProvider);

        await foreach (var _ in firstService.StreamAsync(
            new AssistantChatRequest(
                [new AssistantChatMessage("user", "Try the tool")],
                OrganizationId: "organization-id",
                ConversationId: conversationId),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
        }

        var secondService = CreateAssistantService(secondHandler, appOptions, cache, lockProvider);
        await foreach (var _ in secondService.StreamAsync(
            new AssistantChatRequest(
                [new AssistantChatMessage("user", "What happened last time?")],
                OrganizationId: "organization-id",
                ConversationId: conversationId),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
        }

        Assert.Contains("server-recorded tool results from earlier turns", secondHandler.RequestBody);
        Assert.Contains("unknown_tool", secondHandler.RequestBody);
        Assert.Contains("Unknown tool", secondHandler.RequestBody);

        var isolatedHandler = new StubHttpMessageHandler(
            """
            data: {"choices":[{"delta":{"content":"No prior context."}}]}

            data: [DONE]

            """);
        var isolatedService = CreateAssistantService(isolatedHandler, appOptions, cache, lockProvider);
        await foreach (var _ in isolatedService.StreamAsync(
            new AssistantChatRequest(
                [new AssistantChatMessage("user", "What happened last time?")],
                OrganizationId: "different-organization-id",
                ConversationId: conversationId),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
        }

        Assert.DoesNotContain("server-recorded tool results from earlier turns", isolatedHandler.RequestBody);
    }

    [Fact]
    public async Task StreamAsync_UsageOnlyFinalChunk_RecordsProviderUsage()
    {
        var handler = new StubHttpMessageHandler(
            """
            data: {"choices":[{"delta":{"content":"Answer"}}]}

            data: {"choices":[],"usage":{"prompt_tokens":12000,"completion_tokens":750,"total_tokens":12750,"cost":0.002345}}

            data: [DONE]

            """);
        var appOptions = AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppMode"] = AppMode.Production.ToString(),
                ["BaseURL"] = "https://localhost",
                ["Assistant:ApiKey"] = "test-key"
            })
            .Build());
        using var cache = new InMemoryCacheClient(new InMemoryCacheClientOptions
        {
            LoggerFactory = NullLoggerFactory.Instance,
            TimeProvider = TimeProvider.System
        });
        var lockProvider = CreateLockProvider(cache, TimeProvider.System);
        var usageService = new AssistantUsageService(cache, lockProvider, new RecordingAssistantUsageRecorder(), appOptions, TimeProvider.System, NullLogger<AssistantUsageService>.Instance);
        var service = CreateAssistantService(handler, appOptions, cache, lockProvider, usageService);

        await foreach (var _ in service.StreamAsync(
            new AssistantChatRequest(
                [new AssistantChatMessage("user", "Investigate this")],
                OrganizationId: "organization-id"),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
        }

        var usage = await usageService.GetMonthlyUsageAsync("organization-id");
        Assert.Equal(12_000, usage.PromptTokens);
        Assert.Equal(750, usage.CompletionTokens);
        Assert.Equal(0.002345m, usage.CostUsd);
    }

    [Fact]
    public async Task StreamAsync_EarlyConsumerDisposal_PersistsConservativeProviderUsage()
    {
        var handler = new StubHttpMessageHandler(
            """
            data: {"choices":[{"delta":{"content":"Answer"}}]}

            data: [DONE]

            """);
        var appOptions = AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppMode"] = AppMode.Production.ToString(),
                ["BaseURL"] = "https://localhost",
                ["Assistant:ApiKey"] = "test-key"
            })
            .Build());
        using var cache = new InMemoryCacheClient(new InMemoryCacheClientOptions
        {
            LoggerFactory = NullLoggerFactory.Instance,
            TimeProvider = TimeProvider.System
        });
        var lockProvider = CreateLockProvider(cache, TimeProvider.System);
        var recorder = new RecordingAssistantUsageRecorder();
        var usageService = new AssistantUsageService(cache, lockProvider, recorder, appOptions, TimeProvider.System, NullLogger<AssistantUsageService>.Instance);
        var service = CreateAssistantService(handler, appOptions, cache, lockProvider, usageService);

        await foreach (var _ in service.StreamAsync(
            new AssistantChatRequest(
                [new AssistantChatMessage("user", "Investigate this")],
                OrganizationId: "organization-id"),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
            break;
        }

        var usage = await usageService.GetMonthlyUsageAsync("organization-id");
        Assert.True(usage.PromptTokens > 0);
        Assert.Equal(AssistantLimits.MaximumOutputTokens, usage.CompletionTokens);
        Assert.Contains(recorder.Records, record => record.Increment.PromptTokens > 0
            && record.Increment.CompletionTokens == AssistantLimits.MaximumOutputTokens);
    }

    [Fact]
    public async Task StreamAsync_ProviderError_PersistsConservativeProviderUsage()
    {
        var handler = new StubHttpMessageHandler(
            """
            data: {"error":{"message":"Provider failed"}}

            """);
        var appOptions = AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppMode"] = AppMode.Production.ToString(),
                ["BaseURL"] = "https://localhost",
                ["Assistant:ApiKey"] = "test-key"
            })
            .Build());
        using var cache = new InMemoryCacheClient(new InMemoryCacheClientOptions
        {
            LoggerFactory = NullLoggerFactory.Instance,
            TimeProvider = TimeProvider.System
        });
        var lockProvider = CreateLockProvider(cache, TimeProvider.System);
        var recorder = new RecordingAssistantUsageRecorder();
        var usageService = new AssistantUsageService(cache, lockProvider, recorder, appOptions, TimeProvider.System, NullLogger<AssistantUsageService>.Instance);
        var service = CreateAssistantService(handler, appOptions, cache, lockProvider, usageService);

        await Assert.ThrowsAsync<AssistantProviderException>(async () =>
        {
            await foreach (var _ in service.StreamAsync(
                new AssistantChatRequest(
                    [new AssistantChatMessage("user", "Investigate this")],
                    OrganizationId: "organization-id"),
                "user-id",
                CreatePlanOptions(),
                TestContext.Current.CancellationToken))
            {
            }
        });

        var usage = await usageService.GetMonthlyUsageAsync("organization-id");
        Assert.True(usage.PromptTokens > 0);
        Assert.Equal(AssistantLimits.MaximumOutputTokens, usage.CompletionTokens);
        Assert.Contains(recorder.Records, record => record.Increment.PromptTokens > 0
            && record.Increment.CompletionTokens == AssistantLimits.MaximumOutputTokens);
    }

    [Fact]
    public void Serialize_StackAndProjectResults_AddsWebNavigationLinks()
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureExceptionlessApiDefaults();
        var project = new McpProjectResult(
            "project id",
            "organization-id",
            "Project",
            DateTime.UtcNow,
            DateTime.UtcNow,
            "/api/v2/projects/project-id");
        var stack = new McpStackResult(
            "stack id",
            "organization-id",
            "project-id",
            Event.KnownTypes.Error,
            "open",
            "Stack title",
            10,
            DateTime.UtcNow,
            DateTime.UtcNow,
            [],
            [],
            false,
            DateTime.UtcNow,
            DateTime.UtcNow,
            "/api/v2/stacks/stack-id");
        var ev = new McpEventResult(
            "event id",
            "organization-id",
            "project-id",
            "stack id",
            DateTimeOffset.UtcNow,
            [],
            false,
            DateTime.UtcNow,
            "/api/v2/events/event-id");

        string projects = AssistantToolResultSerializer.Serialize(
            "list_projects",
            McpResponse<McpListData<McpProjectResult>>.Success(new McpListData<McpProjectResult>([project])),
            serializerOptions);
        string stacks = AssistantToolResultSerializer.Serialize(
            "search_stacks",
            McpResponse<McpListData<McpStackResult>>.Success(new McpListData<McpStackResult>([stack])),
            serializerOptions);
        string stackDetails = AssistantToolResultSerializer.Serialize(
            "get_stack",
            McpResponse<McpStackResult>.Success(stack),
            serializerOptions);
        string eventDetails = AssistantToolResultSerializer.Serialize(
            "get_event",
            McpResponse<McpEventResult>.Success(ev),
            serializerOptions);

        Assert.Contains("\"webUrl\":\"/next/project/project%20id/stacks\"", projects);
        Assert.Contains("\"webUrl\":\"/next/stack/stack%20id\"", stacks);
        Assert.Contains("\"webUrl\":\"/next/stack/stack%20id\"", stackDetails);
        Assert.Contains("\"webUrl\":\"/next/stack/stack%20id/event/event%20id\"", eventDetails);
        Assert.Contains("\"url\":\"/api/v2/projects/project-id\"", projects);
        Assert.Contains("\"url\":\"/api/v2/stacks/stack-id\"", stacks);
    }

    [Fact]
    public async Task StreamAsync_EmptyResponse_EmitsClearErrorAndCompletion()
    {
        var handler = new StubHttpMessageHandler("data: [DONE]\n\n");
        var appOptions = AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BaseURL"] = "https://localhost",
                ["Assistant:ApiKey"] = "test-key"
            })
            .Build());
        var service = CreateAssistantService(handler, appOptions);
        var events = new List<AssistantStreamEvent>();

        await foreach (var item in service.StreamAsync(
            new AssistantChatRequest([new AssistantChatMessage("user", "Investigate this")]),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
            events.Add(item);
        }

        Assert.Collection(events,
            item =>
            {
                Assert.Equal("error", item.Type);
                Assert.Equal("Exie stopped before providing an answer. Please try again.", item.Message);
            },
            item => Assert.Equal("done", item.Type));
    }

    [Fact]
    public async Task StreamAsync_ToolBudgetExhausted_RequestsFinalSynthesisWithoutTools()
    {
        const string toolCallResponse = """
            data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call-1","function":{"name":"unknown_tool","arguments":"{}"}}]}}]}

            data: [DONE]

            """;
        var handler = new StubHttpMessageHandler(
            toolCallResponse,
            toolCallResponse.Replace("call-1", "call-2"),
            toolCallResponse.Replace("call-1", "call-3"),
            """
            data: {"choices":[{"delta":{"content":"Here is the available result."}}]}

            data: [DONE]

            """);
        var appOptions = AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BaseURL"] = "https://localhost",
                ["Assistant:ApiKey"] = "test-key"
            })
            .Build());
        var service = CreateAssistantService(handler, appOptions);
        var events = new List<AssistantStreamEvent>();

        await foreach (var item in service.StreamAsync(
            new AssistantChatRequest(
                [new AssistantChatMessage("user", "Investigate the errors")],
                OrganizationId: "organization-id"),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
            events.Add(item);
        }

        Assert.Equal(4, handler.RequestBodies.Count);
        Assert.All(handler.RequestBodies.Take(3), body => Assert.Contains("\"tools\":", body));
        Assert.DoesNotContain("\"tools\":", handler.RequestBodies[3]);
        Assert.Contains("The tool budget is exhausted", handler.RequestBodies[3]);
        Assert.Contains(events, item => item.Text == "Here is the available result.");
        Assert.Equal("done", events[^1].Type);
        Assert.DoesNotContain(events, item => item.Type == "error");
    }

    [Fact]
    public async Task StreamAsync_ParallelToolCalls_EnforcesToolCallLimit()
    {
        var toolCalls = Enumerable.Range(0, AssistantLimits.MaximumToolCallsPerTurn + 1)
            .Select(index => new
            {
                index,
                id = $"call-{index}",
                function = new { name = $"unknown_tool_{index}", arguments = "{}" }
            })
            .ToArray();
        var handler = new StubHttpMessageHandler(
            $"data: {JsonSerializer.Serialize(new { choices = new[] { new { delta = new { tool_calls = toolCalls } } } })}\n\ndata: [DONE]\n",
            """
            data: {"choices":[{"delta":{"content":"I used the result that was available."}}]}

            data: [DONE]

            """);
        var appOptions = AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BaseURL"] = "https://localhost",
                ["Assistant:ApiKey"] = "test-key"
            })
            .Build());
        var service = CreateAssistantService(handler, appOptions);
        var events = new List<AssistantStreamEvent>();

        await foreach (var item in service.StreamAsync(
            new AssistantChatRequest(
                [new AssistantChatMessage("user", "Try both tools")],
                OrganizationId: "organization-id"),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
            events.Add(item);
        }

        var results = events.Where(item => item.Type == "tool_result").ToArray();
        Assert.Equal(AssistantLimits.MaximumToolCallsPerTurn + 1, results.Length);
        Assert.Contains("Unknown tool", results[0].Result);
        Assert.Contains("tool_call_limit_reached", results[^1].Result);
        Assert.Equal("done", events[^1].Type);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static ExceptionlessMcpTools CreateMcpTools() => new(
        null!,
        null!,
        null!,
        null!,
        null!,
        null!,
        null!,
        null!,
        null!,
        null!,
        null!,
        NullLogger<ExceptionlessMcpTools>.Instance,
        TimeProvider.System);

    private static AssistantService CreateAssistantService(
        StubHttpMessageHandler handler,
        AppOptions appOptions,
        ICacheClient? cache = null,
        ILockProvider? lockProvider = null,
        AssistantUsageService? usageService = null)
    {
        cache ??= new InMemoryCacheClient(new InMemoryCacheClientOptions
        {
            LoggerFactory = NullLoggerFactory.Instance,
            TimeProvider = TimeProvider.System
        });
        lockProvider ??= CreateLockProvider(cache, TimeProvider.System);
        usageService ??= new AssistantUsageService(
            cache,
            lockProvider,
            new RecordingAssistantUsageRecorder(),
            appOptions,
            TimeProvider.System,
            NullLogger<AssistantUsageService>.Instance);

        return new AssistantService(
            new StubHttpClientFactory(handler),
            appOptions,
            CreateMcpTools(),
            new AssistantToolContext(),
            new AssistantConversationService(cache, lockProvider, NullLogger<AssistantConversationService>.Instance),
            usageService,
            TimeProvider.System,
            NullLogger<AssistantService>.Instance);
    }

    private static ILockProvider CreateLockProvider(ICacheClient cache, TimeProvider timeProvider)
    {
        var resiliencePolicyProvider = new ResiliencePolicyProvider();
        var serializer = new SystemTextJsonSerializer(new JsonSerializerOptions().ConfigureExceptionlessDefaults());
        var messageBus = new InMemoryMessageBus(new InMemoryMessageBusOptions
        {
            Serializer = serializer,
            TimeProvider = timeProvider,
            ResiliencePolicyProvider = resiliencePolicyProvider,
            LoggerFactory = NullLoggerFactory.Instance
        });
        return new CacheLockProvider(cache, messageBus, timeProvider, resiliencePolicyProvider, NullLoggerFactory.Instance);
    }

    private static AssistantPlanOptions CreatePlanOptions() => new()
    {
        MaximumConcurrentTurns = 2,
        MaximumTurnsPerMinute = 10,
        MaximumMonthlyTokens = 25_000_000,
        MaximumMonthlyCostUsd = 5m
    };

    private sealed class StubHttpMessageHandler(params string[] responseContents) : HttpMessageHandler
    {
        private readonly Queue<string> _responseContents = new(responseContents);
        public string? AuthorizationScheme { get; private set; }
        public string RequestBody => RequestBodies.LastOrDefault() ?? String.Empty;
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseContents.Dequeue(), Encoding.UTF8, "text/event-stream")
            };
        }
    }
}
