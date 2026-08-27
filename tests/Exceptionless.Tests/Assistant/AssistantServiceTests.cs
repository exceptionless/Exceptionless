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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Exceptionless.Tests.Assistant;

public sealed class AssistantServiceTests
{
    [Theory]
    [InlineData("requested-project", "Named project", "current-project", "requested-project")]
    [InlineData(null, "Named project", "current-project", null)]
    [InlineData(null, null, "current-project", "current-project")]
    public void GetProjectSetupProjectId_ExplicitTarget_UsesExpectedPrecedence(
        string? requestedProjectId,
        string? requestedProjectName,
        string? currentProjectId,
        string? expectedProjectId)
    {
        Assert.Equal(
            expectedProjectId,
            AssistantService.GetProjectSetupProjectId(requestedProjectId, requestedProjectName, currentProjectId));
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
        using var providerRequest = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("~deepseek/deepseek-v4-flash-latest", providerRequest.RootElement.GetProperty("model").GetString());
        Assert.Contains($"\"max_tokens\":{AssistantLimits.MaximumOutputTokens}", handler.RequestBody);
        Assert.Contains("get_event", handler.RequestBody);
        Assert.Contains("get_stack", handler.RequestBody);
        Assert.Contains("get_project_setup", handler.RequestBody);
        Assert.Contains("get_stack_events", handler.RequestBody);
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
        Assert.Contains("CLIENT SETUP RULE", handler.RequestBody);
        Assert.Contains("pass that exact projectName to get_project_setup", handler.RequestBody);
        Assert.Contains("Do not call get_stack or list_projects for setup", handler.RequestBody);
        Assert.Contains("Never invent packages or advertise Python, Java, Ruby, or PHP clients", handler.RequestBody);
        Assert.Contains("describe React Native and Expo as supported only when returned by the tool", handler.RequestBody);
        Assert.Contains("do not call list_projects, call each needed project-scoped tool only once", handler.RequestBody);
        Assert.Contains("Defaults to the current page project id when omitted", handler.RequestBody);
        Assert.Contains("This tool has no stack id filter", handler.RequestBody);
        Assert.Contains("Never pass a stackId as eventId", handler.RequestBody);
        Assert.Contains("\"eventId\"", handler.RequestBody);
        Assert.Contains("\"startUtc\"", handler.RequestBody);
        Assert.Contains("\"after\"", handler.RequestBody);
        Assert.Contains("\"maximum\":10", handler.RequestBody);
        Assert.Contains("\"maximum\":16384", handler.RequestBody);
        Assert.DoesNotContain("\"event_id\"", handler.RequestBody);
        Assert.Contains("Never end by merely saying what you will inspect or do next", handler.RequestBody);
        Assert.Contains("present useful results directly in the answer", handler.RequestBody);
        Assert.Contains("use no more than three table columns", handler.RequestBody);
        Assert.Contains("webUrl beginning with / must remain relative", handler.RequestBody);
        Assert.Contains("suggest_followups", handler.RequestBody);
        Assert.Contains("Do not call it on every answer", handler.RequestBody);
        Assert.Contains("MUST call suggest_followups when your answer asks what the user wants to investigate or do next", handler.RequestBody);
        Assert.Contains("Call this whenever the answer asks what the user wants to investigate or do next", handler.RequestBody);
        Assert.Contains("If there is no genuinely useful next step, end the answer directly and omit the tool", handler.RequestBody);

        JsonElement getStackEvents = providerRequest.RootElement.GetProperty("tools").EnumerateArray()
            .Single(tool => tool.GetProperty("function").GetProperty("name").GetString() == "get_stack_events")
            .GetProperty("function");
        JsonElement getStackEventsParameters = getStackEvents.GetProperty("parameters");
        Assert.DoesNotContain(
            getStackEventsParameters.GetProperty("required").EnumerateArray(),
            item => item.GetString() == "stackId");
        Assert.Contains(
            "Defaults to the current page stack id when omitted.",
            getStackEventsParameters.GetProperty("properties").GetProperty("stackId").GetProperty("description").GetString());
        Assert.Equal(
            AssistantLimits.MaximumToolItemsPerCall,
            getStackEventsParameters.GetProperty("properties").GetProperty("limit").GetProperty("maximum").GetInt32());
    }

    [Fact]
    public async Task StreamAsync_RuntimeModelOverride_UsesOverride()
    {
        var handler = new StubHttpMessageHandler(
            """
            data: {"choices":[{"delta":{"content":"Hello"}}]}

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
            LoggerFactory = NullLoggerFactory.Instance,
            TimeProvider = TimeProvider.System
        });
        var settingsService = CreateAssistantModelSettingsService(appOptions);
        await settingsService.SetModelAsync("z-ai/glm-5.3-flash", "000000000000000000000001");
        var service = CreateAssistantService(handler, appOptions, cache, modelSettingsService: settingsService);

        await foreach (var _ in service.StreamAsync(
            new AssistantChatRequest([new AssistantChatMessage("user", "Say hello")]),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
        }

        using var providerRequest = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("z-ai/glm-5.3-flash", providerRequest.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task StreamAsync_ExplicitWriteRequest_ExecutesToolWithoutConfirmationGate()
    {
        string toolCallPayload = JsonSerializer.Serialize(new
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
                                id = "snooze-1",
                                function = new
                                {
                                    name = "snooze_stack",
                                    arguments = JsonSerializer.Serialize(new { stackId = "invalid", duration = "7d" })
                                }
                            }
                        }
                    }
                }
            }
        });
        var handler = new StubHttpMessageHandler(
            $"data: {toolCallPayload}\n\ndata: [DONE]\n",
            """
            data: {"choices":[{"delta":{"content":"I attempted the requested update."}}]}

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
                [new AssistantChatMessage("user", "Snooze them for 7 days while I investigate.")],
                OrganizationId: "organization-id"),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
            events.Add(item);
        }

        var toolResult = Assert.Single(events, item => item.Type == "tool_result");
        Assert.Contains($"\"code\":\"{McpErrorCodes.InvalidId}\"", toolResult.Result);
        Assert.DoesNotContain("write_confirmation_required", toolResult.Result);
        Assert.Contains(events, item => item.Text == "I attempted the requested update.");
        Assert.Equal("done", events[^1].Type);
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
    public async Task StreamAsync_ConfigureSuggestedAction_EmitsValidatedInternalHref()
    {
        string configureHref = AssistantRoutes.ProjectConfigure("project-id");
        string providerPayload = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    delta = new
                    {
                        content = "Use Client Setup for the verified instructions.",
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
                                        actions = new object[]
                                        {
                                            new { label = "Open Client Setup", href = configureHref },
                                            new { label = "Unsafe link", href = AssistantRoutes.ProjectConfigure("another-project") },
                                            new
                                            {
                                                label = "Ambiguous action",
                                                prompt = "Configure this project",
                                                href = configureHref
                                            }
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
            new AssistantChatRequest([new AssistantChatMessage("user", "How do I configure this project?")], ProjectId: "project-id"),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
            events.Add(item);
        }

        var action = Assert.Single(Assert.Single(events, item => item.Type == "suggested_actions").SuggestedActions!);
        Assert.Equal("Open Client Setup", action.Label);
        Assert.Equal(configureHref, action.Href);
        Assert.Equal("How do I configure this project to start sending events?", action.Prompt);
    }

    [Fact]
    public void GetProjectSetupHref_ResolvedProject_ReturnsCanonicalHref()
    {
        string configureHref = AssistantRoutes.ProjectConfigure("resolved-project");
        string result = JsonSerializer.Serialize(new
        {
            ok = true,
            data = new { id = "resolved-project", webUrl = configureHref }
        });

        Assert.Equal(configureHref, AssistantSuggestedActionParser.GetProjectSetupHref(result));
    }

    [Fact]
    public void GetProjectSetupHref_MismatchedRoute_ReturnsNull()
    {
        string result = JsonSerializer.Serialize(new
        {
            ok = true,
            data = new { id = "resolved-project", webUrl = AssistantRoutes.ProjectConfigure("other-project") }
        });

        Assert.Null(AssistantSuggestedActionParser.GetProjectSetupHref(result));
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
    public async Task StreamAsync_ProviderRejectsRequest_ReleasesReservationWithoutChargingUsage()
    {
        var handler = new RejectedHttpMessageHandler(HttpStatusCode.Unauthorized);
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
        Assert.Equal(0, usage.PromptTokens);
        Assert.Equal(0, usage.CompletionTokens);
        Assert.Equal(0, usage.CostInMicrodollars);
        Assert.Contains(recorder.Records, record => record.Increment.ProviderRequests == 1);
        Assert.DoesNotContain(recorder.Records, record => record.Increment.PromptTokens > 0 || record.Increment.CompletionTokens > 0);
    }

    [Fact]
    public async Task StreamAsync_OversizedProviderInput_DoesNotReserveProviderUsage()
    {
        var handler = new StubHttpMessageHandler("data: [DONE]\n\n");
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
                    [new AssistantChatMessage("user", new string('\u0001', AssistantLimits.MaximumInputCharacters))],
                    OrganizationId: "organization-id"),
                "user-id",
                CreatePlanOptions(),
                TestContext.Current.CancellationToken))
            {
            }
        });

        var usage = await usageService.GetMonthlyUsageAsync("organization-id");
        Assert.Equal(0, usage.PromptTokens);
        Assert.Equal(0, usage.CompletionTokens);
        Assert.DoesNotContain(recorder.Records, record => record.Increment.ProviderRequests > 0);
        Assert.Empty(handler.RequestBodies);
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
        string stackEvents = AssistantToolResultSerializer.Serialize(
            "get_stack_events",
            McpResponse<McpListData<McpEventResult>>.Success(new McpListData<McpEventResult>([ev])),
            serializerOptions);

        Assert.Contains($"\"webUrl\":\"{AssistantRoutes.ProjectStacks("project id")}\"", projects);
        Assert.Contains($"\"webUrl\":\"{AssistantRoutes.Stack("stack id")}\"", stacks);
        Assert.Contains($"\"webUrl\":\"{AssistantRoutes.Stack("stack id")}\"", stackDetails);
        Assert.Contains($"\"webUrl\":\"{AssistantRoutes.Event("stack id", "event id")}\"", eventDetails);
        Assert.Contains($"\"webUrl\":\"{AssistantRoutes.Event("stack id", "event id")}\"", stackEvents);
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
    public async Task StreamAsync_RawDsmlResponse_RetriesWithoutEmittingMarkup()
    {
        string recoveredPayload = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    delta = new
                    {
                        content = "I found no errors in the selected time range.",
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
                                            new { label = "Search seven days", prompt = "Search for errors in the last 7 days." }
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
            """
            data: {"choices":[{"delta":{"content":"I found no errors. "}}]}

            data: {"choices":[{"delta":{"content":"<｜DS"}}]}

            data: {"choices":[{"delta":{"content":"ML｜tool_calls><｜DSML｜invoke name=\"suggest_followups\">"}}]}

            data: [DONE]

            """,
            $"data: {recoveredPayload}\n\ndata: [DONE]\n");
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
            new AssistantChatRequest([new AssistantChatMessage("user", "Find recent errors")]),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
            events.Add(item);
        }

        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Contains("The previous provider response exposed internal DSML tool-call markup as text", handler.RequestBodies[1]);
        Assert.Collection(events,
            item => Assert.Equal("I found no errors in the selected time range.", item.Text),
            item =>
            {
                Assert.Equal("suggested_actions", item.Type);
                Assert.Equal("Search seven days", Assert.Single(item.SuggestedActions!).Label);
            },
            item => Assert.Equal("done", item.Type));
        Assert.DoesNotContain("DSML", String.Concat(events.Select(item => item.Text)), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("I found no errors. ", String.Concat(events.Select(item => item.Text)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StreamAsync_RepeatedRawDsmlResponse_EmitsClearErrorAndCompletion()
    {
        const string malformedResponse = """
            data: {"choices":[{"delta":{"content":"<|DSML|tool_calls>"}}]}

            data: [DONE]

            """;
        var handler = new StubHttpMessageHandler(malformedResponse, malformedResponse);
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
            new AssistantChatRequest([new AssistantChatMessage("user", "Find recent errors")]),
            "user-id",
            CreatePlanOptions(),
            TestContext.Current.CancellationToken))
        {
            events.Add(item);
        }

        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Collection(events,
            item =>
            {
                Assert.Equal("error", item.Type);
                Assert.Equal("Exie received a malformed response from the AI provider. Please try again.", item.Message);
            },
            item => Assert.Equal("done", item.Type));
        Assert.DoesNotContain(events, item => item.Type == "text_delta");
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

    private static ExceptionlessMcpTools CreateMcpTools(AssistantToolContext assistantToolContext) => new(
        new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
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
        TimeProvider.System,
        assistantToolContext);

    private static AssistantService CreateAssistantService(
        HttpMessageHandler handler,
        AppOptions appOptions,
        ICacheClient? cache = null,
        ILockProvider? lockProvider = null,
        AssistantUsageService? usageService = null,
        AssistantModelSettingsService? modelSettingsService = null)
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
        modelSettingsService ??= CreateAssistantModelSettingsService(appOptions);

        var assistantToolContext = new AssistantToolContext();
        return new AssistantService(
            new StubHttpClientFactory(handler),
            appOptions,
            CreateMcpTools(assistantToolContext),
            assistantToolContext,
            new AssistantConversationService(cache, lockProvider, NullLogger<AssistantConversationService>.Instance),
            modelSettingsService,
            usageService,
            TimeProvider.System,
            NullLogger<AssistantService>.Instance);
    }

    private static AssistantModelSettingsService CreateAssistantModelSettingsService(AppOptions appOptions)
    {
        SystemSettings? settings = null;
        return new AssistantModelSettingsService(
            () => Task.FromResult(settings),
            value =>
            {
                settings = value;
                return Task.CompletedTask;
            },
            appOptions,
            TimeProvider.System);
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

    private sealed class RejectedHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{\"error\":{\"message\":\"Rejected\"}}", Encoding.UTF8, "application/json")
            });
    }
}
