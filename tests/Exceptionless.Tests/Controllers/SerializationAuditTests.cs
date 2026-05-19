using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elastic.Clients.Elasticsearch;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Controllers;

/// <summary>
/// Serialization audit tests that submit payloads with different JSON casing conventions
/// to critical API endpoints and capture request/elasticsearch/response JSON files.
/// These files are saved to branch-specific folders for diffing between branches.
/// </summary>
public sealed class SerializationAuditTests : IntegrationTestsBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly IEventRepository _eventRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IQueue<EventPost> _eventQueue;
    private readonly ExceptionlessElasticConfiguration _esConfiguration;

    /// <summary>
    /// Base output directory. Set AUDIT_RUN_ID env var to use a dated sub-folder, e.g.:
    ///   AUDIT_RUN_ID=post-fixes → audit-output/post-fixes/{branch}/
    /// If not set, falls back to audit-output/{branch}/ (original behavior).
    /// </summary>
    private static readonly string s_outputDir = GetOutputDir();

    private static string GetOutputDir()
    {
        string root = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "..", "audit-output"));
        string? runId = Environment.GetEnvironmentVariable("AUDIT_RUN_ID");
        return string.IsNullOrWhiteSpace(runId) ? root : Path.Combine(root, runId);
    }

    public SerializationAuditTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _jsonSerializerOptions = GetService<JsonSerializerOptions>();
        _eventRepository = GetService<IEventRepository>();
        _stackRepository = GetService<IStackRepository>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _eventQueue = GetService<IQueue<EventPost>>();
        _esConfiguration = GetService<ExceptionlessElasticConfiguration>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await _eventQueue.DeleteQueueAsync();

        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    private string GetBranchOutputDir()
    {
        // Detect current git branch
        string branch = "feature-system-text-json-v2";
        try
        {
            var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --abbrev-ref HEAD",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (proc is not null)
            {
                branch = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit();
            }
        }
        catch { /* fallback to hardcoded */ }

        // Sanitize branch name for filesystem
        branch = branch.Replace("/", "-").Replace("\\", "-");
        return Path.Combine(s_outputDir, branch);
    }

    private Task SaveAuditFileAsync(string testName, string suffix, string json)
    {
        string dir = Path.Combine(GetBranchOutputDir(), testName);
        Directory.CreateDirectory(dir);
        string filePath = Path.Combine(dir, suffix);
        return File.WriteAllTextAsync(filePath, PrettyPrint(json));
    }

    private static string PrettyPrint(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }

    /// <summary>
    /// Get the raw JSON document from Elasticsearch by ID for a given index pattern.
    /// </summary>
    private async Task<string> GetElasticsearchDocumentAsync(string index, string id)
    {
        var client = _esConfiguration.Client;
        var response = await client.GetAsync<JsonElement>(id, g => g.Index(index));
        if (response.IsValidResponse && response.Source.ValueKind != JsonValueKind.Undefined)
        {
            return JsonSerializer.Serialize(response.Source, new JsonSerializerOptions { WriteIndented = true });
        }

        // Fallback: search by id across index pattern
        var searchResponse = await client.SearchAsync<JsonElement>(s => s
            .Indices(index)
            .Query(q => q.Ids(ids => ids.Values(new[] { id })))
            .Size(1));

        if (searchResponse.IsValidResponse && searchResponse.Documents.Count > 0)
        {
            return JsonSerializer.Serialize(searchResponse.Documents.First(), new JsonSerializerOptions { WriteIndented = true });
        }

        return $"{{\"error\": \"Document {id} not found in index {index}\"}}";
    }

    private string GetEventsIndexPattern()
    {
        // Events use daily indices like: {scope}-events-v1-{date}
        return $"*-events-*";
    }

    private string GetStacksIndexPattern()
    {
        return $"*-stacks-*";
    }

    private string GetOrganizationsIndexPattern()
    {
        return $"*-organizations-*";
    }

    // EVENT ENDPOINT TESTS - Different casing variants

    [Fact]
    public async Task Events_SnakeCase_FullPayload()
    {
        const string testName = "events-post-snake-case";
        /* language=json */
        const string requestJson = """
        {
            "type": "error",
            "message": "Test error with snake_case payload",
            "date": "2026-05-20T12:00:00+00:00",
            "tags": ["audit", "snake_case"],
            "reference_id": "audit-snake-001",
            "count": 1,
            "value": 42.5,
            "geo": "40.7128,-74.0060",
            "data": {
                "custom_field": "custom_value",
                "nested_object": {
                    "inner_key": "inner_value",
                    "inner_number": 123
                }
            },
            "@user": {
                "identity": "user@example.com",
                "name": "Test User",
                "data": {
                    "plan_name": "premium"
                }
            },
            "@environment": {
                "o_s_name": "Windows 11",
                "o_s_version": "10.0.22621",
                "ip_address": "192.168.1.100",
                "machine_name": "AUDIT-MACHINE",
                "runtime_version": ".NET 8.0.1",
                "processor_count": 8,
                "total_physical_memory": 17179869184,
                "available_physical_memory": 8589934592,
                "process_name": "AuditApp",
                "process_id": "12345",
                "process_memory_size": 104857600,
                "thread_id": "1",
                "command_line": "AuditApp.exe --test"
            },
            "@request": {
                "client_ip_address": "10.0.0.100",
                "http_method": "POST",
                "user_agent": "AuditAgent/1.0",
                "is_secure": true,
                "host": "audit.localhost",
                "path": "/api/audit?key=value&other=123",
                "port": 443,
                "query_string": {
                    "key": "value",
                    "special_chars": "<script>alert('xss')</script>"
                },
                "cookies": {
                    "session_id": "abc123"
                }
            },
            "@simple_error": {
                "message": "Null reference exception occurred",
                "type": "System.NullReferenceException",
                "stack_trace": "   at Audit.Tests.Run() in AuditTests.cs:line 42\n   at Audit.Main() in Program.cs:line 10"
            }
        }
        """;

        await SaveAuditFileAsync(testName, "request.json", requestJson);
        await SubmitAndCaptureEventAsync(testName, requestJson);
    }

    [Fact]
    public async Task Events_CamelCase_FullPayload()
    {
        const string testName = "events-post-camel-case";
        /* language=json */
        const string requestJson = """
        {
            "type": "error",
            "message": "Test error with camelCase payload",
            "date": "2026-05-20T12:00:00+00:00",
            "tags": ["audit", "camelCase"],
            "referenceId": "audit-camel-001",
            "count": 1,
            "value": 42.5,
            "geo": "40.7128,-74.0060",
            "data": {
                "customField": "custom_value",
                "nestedObject": {
                    "innerKey": "inner_value",
                    "innerNumber": 123
                }
            },
            "@user": {
                "identity": "user@example.com",
                "name": "Test User",
                "data": {
                    "planName": "premium"
                }
            },
            "@environment": {
                "osName": "Windows 11",
                "osVersion": "10.0.22621",
                "ipAddress": "192.168.1.100",
                "machineName": "AUDIT-MACHINE",
                "runtimeVersion": ".NET 8.0.1",
                "processorCount": 8,
                "totalPhysicalMemory": 17179869184,
                "availablePhysicalMemory": 8589934592,
                "processName": "AuditApp",
                "processId": "12345",
                "processMemorySize": 104857600,
                "threadId": "1",
                "commandLine": "AuditApp.exe --test"
            },
            "@request": {
                "clientIpAddress": "10.0.0.100",
                "httpMethod": "POST",
                "userAgent": "AuditAgent/1.0",
                "isSecure": true,
                "host": "audit.localhost",
                "path": "/api/audit?key=value&other=123",
                "port": 443,
                "queryString": {
                    "key": "value",
                    "specialChars": "<script>alert('xss')</script>"
                },
                "cookies": {
                    "sessionId": "abc123"
                }
            },
            "@simpleError": {
                "message": "Null reference exception occurred",
                "type": "System.NullReferenceException",
                "stackTrace": "   at Audit.Tests.Run() in AuditTests.cs:line 42\n   at Audit.Main() in Program.cs:line 10"
            }
        }
        """;

        await SaveAuditFileAsync(testName, "request.json", requestJson);
        await SubmitAndCaptureEventAsync(testName, requestJson);
    }

    [Fact]
    public async Task Events_PascalCase_FullPayload()
    {
        const string testName = "events-post-pascal-case";
        /* language=json */
        const string requestJson = """
        {
            "Type": "error",
            "Message": "Test error with PascalCase payload",
            "Date": "2026-05-20T12:00:00+00:00",
            "Tags": ["audit", "PascalCase"],
            "ReferenceId": "audit-pascal-001",
            "Count": 1,
            "Value": 42.5,
            "Geo": "40.7128,-74.0060",
            "Data": {
                "CustomField": "custom_value",
                "NestedObject": {
                    "InnerKey": "inner_value",
                    "InnerNumber": 123
                }
            },
            "@user": {
                "Identity": "user@example.com",
                "Name": "Test User",
                "Data": {
                    "PlanName": "premium"
                }
            },
            "@environment": {
                "OSName": "Windows 11",
                "OSVersion": "10.0.22621",
                "IpAddress": "192.168.1.100",
                "MachineName": "AUDIT-MACHINE",
                "RuntimeVersion": ".NET 8.0.1",
                "ProcessorCount": 8,
                "TotalPhysicalMemory": 17179869184,
                "AvailablePhysicalMemory": 8589934592,
                "ProcessName": "AuditApp",
                "ProcessId": "12345",
                "ProcessMemorySize": 104857600,
                "ThreadId": "1",
                "CommandLine": "AuditApp.exe --test"
            },
            "@request": {
                "ClientIpAddress": "10.0.0.100",
                "HttpMethod": "POST",
                "UserAgent": "AuditAgent/1.0",
                "IsSecure": true,
                "Host": "audit.localhost",
                "Path": "/api/audit?key=value&other=123",
                "Port": 443,
                "QueryString": {
                    "key": "value",
                    "SpecialChars": "<script>alert('xss')</script>"
                },
                "Cookies": {
                    "SessionId": "abc123"
                }
            },
            "@simple_error": {
                "Message": "Null reference exception occurred",
                "Type": "System.NullReferenceException",
                "StackTrace": "   at Audit.Tests.Run() in AuditTests.cs:line 42\n   at Audit.Main() in Program.cs:line 10"
            }
        }
        """;

        await SaveAuditFileAsync(testName, "request.json", requestJson);
        await SubmitAndCaptureEventAsync(testName, requestJson);
    }

    [Fact]
    public async Task Events_MixedCase_FullPayload()
    {
        const string testName = "events-post-mixed-case";
        /* language=json */
        const string requestJson = """
        {
            "TYPE": "error",
            "message": "Test error with MIXED casing",
            "Date": "2026-05-20T12:00:00+00:00",
            "TAGS": ["audit", "MIXED"],
            "reference_id": "audit-mixed-001",
            "COUNT": 1,
            "value": 42.5,
            "GEO": "40.7128,-74.0060",
            "data": {
                "CUSTOM_FIELD": "custom_value",
                "nestedObject": {
                    "INNER_KEY": "inner_value",
                    "innerNumber": 123
                }
            },
            "@user": {
                "IDENTITY": "user@example.com",
                "name": "Test User"
            },
            "@environment": {
                "O_S_NAME": "Windows 11",
                "osVersion": "10.0.22621",
                "IP_ADDRESS": "192.168.1.100",
                "machineName": "AUDIT-MACHINE"
            },
            "@request": {
                "CLIENT_IP_ADDRESS": "10.0.0.100",
                "httpMethod": "POST",
                "USER_AGENT": "AuditAgent/1.0",
                "isSecure": true,
                "HOST": "audit.localhost",
                "path": "/api/audit",
                "PORT": 443
            },
            "@simple_error": {
                "MESSAGE": "Null reference exception",
                "type": "System.NullReferenceException",
                "STACK_TRACE": "   at Audit.Tests.Run() in AuditTests.cs:line 42"
            }
        }
        """;

        await SaveAuditFileAsync(testName, "request.json", requestJson);
        await SubmitAndCaptureEventAsync(testName, requestJson);
    }

    [Fact]
    public async Task Events_SpecialCharacters_Payload()
    {
        const string testName = "events-post-special-chars";
        /* language=json */
        const string requestJson = """
        {
            "type": "error",
            "message": "A potentially dangerous Request.Path value was detected from the client (&).",
            "date": "2026-05-20T12:00:00+00:00",
            "tags": ["<script>", "special&chars", "quotes\"here"],
            "reference_id": "audit-special-001",
            "data": {
                "html_content": "<div class=\"test\">Hello & World</div>",
                "url": "https://example.com/path?a=1&b=2&c=<value>",
                "unicode_text": "日本語テスト 🎉 émojis café",
                "null_bytes": "before\u0000after",
                "apostrophe": "it's a test",
                "backslash": "path\\to\\file"
            },
            "@simple_error": {
                "message": "Error: 'Failed' at <Module>::Method(int& param)",
                "type": "System.InvalidOperationException",
                "stack_trace": "   at Namespace.Class.Method(String& value) in C:\\Users\\test\\file.cs:line 10"
            }
        }
        """;

        await SaveAuditFileAsync(testName, "request.json", requestJson);
        await SubmitAndCaptureEventAsync(testName, requestJson);
    }

    [Fact]
    public async Task Events_NumericEdgeCases_Payload()
    {
        const string testName = "events-post-numeric-edge-cases";
        /* language=json */
        const string requestJson = """
        {
            "type": "log",
            "message": "Numeric edge cases test",
            "date": "2026-05-20T12:00:00+00:00",
            "tags": ["audit", "numeric"],
            "reference_id": "audit-numeric-001",
            "count": 0,
            "value": 0.0,
            "data": {
                "int_max": 2147483647,
                "int_min": -2147483648,
                "long_max": 9223372036854775807,
                "long_min": -9223372036854775808,
                "double_val": 1.7976931348623157e308,
                "small_decimal": 0.000000001,
                "negative_zero": -0.0,
                "large_exponent": 1e100,
                "zero_int": 0,
                "zero_float": 0.0,
                "one_point_zero": 1.0
            }
        }
        """;

        await SaveAuditFileAsync(testName, "request.json", requestJson);
        await SubmitAndCaptureEventAsync(testName, requestJson);
    }

    [Fact]
    public async Task Events_NullAndEmpty_Payload()
    {
        const string testName = "events-post-null-empty";
        /* language=json */
        const string requestJson = """
        {
            "type": "log",
            "message": "Null and empty values test",
            "date": "2026-05-20T12:00:00+00:00",
            "tags": [],
            "reference_id": "audit-null-001",
            "count": null,
            "value": null,
            "geo": null,
            "data": {
                "null_value": null,
                "empty_string": "",
                "empty_array": [],
                "empty_object": {},
                "nested_nulls": {
                    "a": null,
                    "b": "",
                    "c": []
                }
            },
            "@user": null
        }
        """;

        await SaveAuditFileAsync(testName, "request.json", requestJson);
        await SubmitAndCaptureEventAsync(testName, requestJson);
    }

    [Fact]
    public async Task Events_DateFormats_Payload()
    {
        const string testName = "events-post-date-formats";
        /* language=json */
        const string requestJson = """
        {
            "type": "log",
            "message": "Date format variations test",
            "date": "2026-05-20T12:00:00.1234567+05:30",
            "tags": ["audit", "dates"],
            "reference_id": "audit-dates-001",
            "data": {
                "iso_utc": "2026-05-20T12:00:00Z",
                "iso_offset": "2026-05-20T12:00:00+05:30",
                "iso_no_tz": "2026-05-20T12:00:00",
                "iso_millis": "2026-05-20T12:00:00.123Z",
                "iso_micros": "2026-05-20T12:00:00.123456Z",
                "epoch_seconds": 1736942400,
                "epoch_millis": 1736942400000,
                "date_only": "2026-01-15",
                "not_a_date": "2026-13-45T99:99:99Z"
            }
        }
        """;

        await SaveAuditFileAsync(testName, "request.json", requestJson);
        await SubmitAndCaptureEventAsync(testName, requestJson);
    }

    // ORGANIZATION ENDPOINT TESTS

    [Fact]
    public async Task Organizations_GetById()
    {
        const string testName = "organizations-get-by-id";
        // Use the test org that SampleDataService creates
        var response = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("organizations")
            .AppendPath(SampleDataService.TEST_ORG_ID)
            .StatusCodeShouldBeOk()
        );

        string responseJson = await response.Content.ReadAsStringAsync(TestCancellationToken);
        await SaveAuditFileAsync(testName, "response.json", responseJson);
    }

    [Fact]
    public async Task Organizations_PatchSnakeCase()
    {
        const string testName = "organizations-patch-snake-case";
        /* language=json */
        const string requestJson = """
        {
            "name": "Updated Org Name (snake_case audit)"
        }
        """;

        await SaveAuditFileAsync(testName, "request.json", requestJson);

        var response = await SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPath("organizations")
            .AppendPath(SampleDataService.TEST_ORG_ID)
            .Content(requestJson, "application/json")
            .StatusCodeShouldBeOk()
        );

        string responseJson = await response.Content.ReadAsStringAsync(TestCancellationToken);
        await SaveAuditFileAsync(testName, "response.json", responseJson);

        // Get ES document
        string esDoc = await GetElasticsearchDocumentAsync(GetOrganizationsIndexPattern(), SampleDataService.TEST_ORG_ID);
        await SaveAuditFileAsync(testName, "elastic.json", esDoc);
    }

    [Fact]
    public async Task Organizations_PatchCamelCase()
    {
        const string testName = "organizations-patch-camel-case";
        /* language=json */
        const string requestJson = """
        {
            "Name": "Updated Org Name (CamelCase audit)"
        }
        """;

        await SaveAuditFileAsync(testName, "request.json", requestJson);

        var response = await SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPath("organizations")
            .AppendPath(SampleDataService.TEST_ORG_ID)
            .Content(requestJson, "application/json")
            .StatusCodeShouldBeOk()
        );

        string responseJson = await response.Content.ReadAsStringAsync(TestCancellationToken);
        await SaveAuditFileAsync(testName, "response.json", responseJson);

        string esDoc = await GetElasticsearchDocumentAsync(GetOrganizationsIndexPattern(), SampleDataService.TEST_ORG_ID);
        await SaveAuditFileAsync(testName, "elastic.json", esDoc);
    }

    // PROJECT ENDPOINT TESTS

    [Fact]
    public async Task Projects_GetById()
    {
        const string testName = "projects-get-by-id";
        var response = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("projects")
            .AppendPath(SampleDataService.TEST_PROJECT_ID)
            .StatusCodeShouldBeOk()
        );

        string responseJson = await response.Content.ReadAsStringAsync(TestCancellationToken);
        await SaveAuditFileAsync(testName, "response.json", responseJson);

        string esDoc = await GetElasticsearchDocumentAsync("*-projects-*", SampleDataService.TEST_PROJECT_ID);
        await SaveAuditFileAsync(testName, "elastic.json", esDoc);
    }

    [Fact]
    public async Task Projects_PatchSnakeCase()
    {
        const string testName = "projects-patch-snake-case";
        /* language=json */
        const string requestJson = """
        {
            "name": "Updated Project (snake_case audit)",
            "delete_bot_data_enabled": true
        }
        """;

        await SaveAuditFileAsync(testName, "request.json", requestJson);

        var response = await SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPath("projects")
            .AppendPath(SampleDataService.TEST_PROJECT_ID)
            .Content(requestJson, "application/json")
            .StatusCodeShouldBeOk()
        );

        string responseJson = await response.Content.ReadAsStringAsync(TestCancellationToken);
        await SaveAuditFileAsync(testName, "response.json", responseJson);

        string esDoc = await GetElasticsearchDocumentAsync("*-projects-*", SampleDataService.TEST_PROJECT_ID);
        await SaveAuditFileAsync(testName, "elastic.json", esDoc);
    }

    // STACK ENDPOINT TESTS

    [Fact]
    public async Task Stacks_GetAfterEventPost()
    {
        const string testName = "stacks-get-after-event";
        /* language=json */
        const string eventJson = """
        {
            "type": "error",
            "message": "Stack audit test error",
            "date": "2026-05-20T12:00:00+00:00",
            "tags": ["audit", "stack-test"],
            "reference_id": "audit-stack-001",
            "@simple_error": {
                "message": "Stack test exception",
                "type": "System.ArgumentException",
                "stack_trace": "   at StackAudit.Test() in Test.cs:line 1"
            }
        }
        """;

        await SaveAuditFileAsync(testName, "event-request.json", eventJson);

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events")
            .Content(eventJson, "application/json")
            .StatusCodeShouldBeAccepted()
        );

        var processEventsJob = GetService<EventPostsJob>();
        await processEventsJob.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Get the event and its stack
        var events = await _eventRepository.GetAllAsync();
        var ev = events.Documents.FirstOrDefault(e => e.ReferenceId == "audit-stack-001");
        Assert.NotNull(ev);

        // Get stack via API
        var stackResponse = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("stacks")
            .AppendPath(ev.StackId)
            .StatusCodeShouldBeOk()
        );

        string stackResponseJson = await stackResponse.Content.ReadAsStringAsync(TestCancellationToken);
        await SaveAuditFileAsync(testName, "stack-response.json", stackResponseJson);

        // Get stack from ES
        string stackEsDoc = await GetElasticsearchDocumentAsync(GetStacksIndexPattern(), ev.StackId);
        await SaveAuditFileAsync(testName, "stack-elastic.json", stackEsDoc);

        // Get event via API
        var eventResponse = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .AppendPath(ev.Id)
            .StatusCodeShouldBeOk()
        );

        string eventResponseJson = await eventResponse.Content.ReadAsStringAsync(TestCancellationToken);
        await SaveAuditFileAsync(testName, "event-response.json", eventResponseJson);

        // Get event from ES
        string eventEsDoc = await GetElasticsearchDocumentAsync(GetEventsIndexPattern(), ev.Id);
        await SaveAuditFileAsync(testName, "event-elastic.json", eventEsDoc);
    }

    // TOKEN ENDPOINT TESTS

    [Fact]
    public async Task Tokens_CreateAndGet()
    {
        const string testName = "tokens-create-and-get";
        /* language=json */
        const string requestJson = """
        {
            "organization_id": "537650f3b77efe23a47914f3",
            "project_id": "537650f3b77efe23a47914f4",
            "scopes": ["client"]
        }
        """;

        await SaveAuditFileAsync(testName, "request.json", requestJson);

        var response = await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("tokens")
            .Content(requestJson, "application/json")
            .StatusCodeShouldBeCreated()
        );

        string responseJson = await response.Content.ReadAsStringAsync(TestCancellationToken);
        await SaveAuditFileAsync(testName, "create-response.json", responseJson);

        // Parse token ID from response
        var tokenDoc = JsonDocument.Parse(responseJson);
        string tokenId = tokenDoc.RootElement.GetProperty("id").GetString()!;

        // Get token back
        var getResponse = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("tokens")
            .AppendPath(tokenId)
            .StatusCodeShouldBeOk()
        );

        string getResponseJson = await getResponse.Content.ReadAsStringAsync(TestCancellationToken);
        await SaveAuditFileAsync(testName, "get-response.json", getResponseJson);
    }

    // WEBHOOK ENDPOINT TESTS

    [Fact]
    public async Task WebHooks_CreateSnakeCase()
    {
        const string testName = "webhooks-create-snake-case";
        /* language=json */
        const string requestJson = """
        {
            "organization_id": "537650f3b77efe23a47914f3",
            "project_id": "537650f3b77efe23a47914f4",
            "url": "https://example.com/webhook",
            "event_types": ["NewEvent", "CriticalEvent", "StackRegression"]
        }
        """;

        await SaveAuditFileAsync(testName, "request.json", requestJson);

        var response = await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("webhooks")
            .Content(requestJson, "application/json")
            .StatusCodeShouldBeCreated()
        );

        string responseJson = await response.Content.ReadAsStringAsync(TestCancellationToken);
        await SaveAuditFileAsync(testName, "create-response.json", responseJson);
    }

    [Fact]
    public async Task WebHooks_CreateCamelCase()
    {
        const string testName = "webhooks-create-camel-case";
        /* language=json */
        const string requestJson = """
        {
            "organizationId": "537650f3b77efe23a47914f3",
            "projectId": "537650f3b77efe23a47914f4",
            "url": "https://example.com/webhook-camel",
            "eventTypes": ["NewEvent", "CriticalEvent"]
        }
        """;

        await SaveAuditFileAsync(testName, "request.json", requestJson);

        // Note: camelCase properties may not be recognized by the snake_case naming policy.
        // This test captures the actual response (which may be an error) for audit diffing.
        var response = await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("webhooks")
            .Content(requestJson, "application/json")
            .StatusCodeShouldBeBadRequest()
        );

        string responseJson = await response.Content.ReadAsStringAsync(TestCancellationToken);
        await SaveAuditFileAsync(testName, "create-response.json", responseJson);
    }

    // EVENT GET RESPONSE FORMAT TESTS

    [Fact]
    public async Task Events_GetList_ResponseFormat()
    {
        const string testName = "events-get-list";

        // Submit an event first
        /* language=json */
        const string eventJson = """
        {
            "type": "log",
            "message": "List format test event",
            "date": "2026-05-20T12:00:00+00:00",
            "reference_id": "audit-list-001",
            "tags": ["audit"]
        }
        """;

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events")
            .Content(eventJson, "application/json")
            .StatusCodeShouldBeAccepted()
        );

        var processEventsJob = GetService<EventPostsJob>();
        await processEventsJob.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Get events list
        var response = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .StatusCodeShouldBeOk()
        );

        string responseJson = await response.Content.ReadAsStringAsync(TestCancellationToken);
        await SaveAuditFileAsync(testName, "response.json", responseJson);
    }

    [Fact]
    public async Task Events_GetWithStackMode_ResponseFormat()
    {
        const string testName = "events-get-stack-mode";

        // Submit events
        /* language=json */
        const string eventJson = """
        {
            "type": "error",
            "message": "Stack mode test",
            "date": "2026-05-20T12:00:00+00:00",
            "reference_id": "audit-stackmode-001",
            "@simple_error": {
                "message": "Stack mode test error",
                "type": "System.Exception"
            }
        }
        """;

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events")
            .Content(eventJson, "application/json")
            .StatusCodeShouldBeAccepted()
        );

        var processEventsJob = GetService<EventPostsJob>();
        await processEventsJob.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Get events in stack_new mode
        var response = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .QueryString("mode", "stack_new")
            .StatusCodeShouldBeOk()
        );

        string responseJson = await response.Content.ReadAsStringAsync(TestCancellationToken);
        await SaveAuditFileAsync(testName, "response.json", responseJson);
    }

    // HELPER: Submit event and capture all artifacts

    private async Task SubmitAndCaptureEventAsync(string testName, string requestJson)
    {
        // Extract reference_id from request to find our specific event
        string? referenceId = null;
        try
        {
            var requestDoc = JsonDocument.Parse(requestJson);
            if (requestDoc.RootElement.TryGetProperty("reference_id", out var refProp))
                referenceId = refProp.GetString();
            else if (requestDoc.RootElement.TryGetProperty("referenceId", out refProp))
                referenceId = refProp.GetString();
            else if (requestDoc.RootElement.TryGetProperty("ReferenceId", out refProp))
                referenceId = refProp.GetString();
        }
        catch { /* ignore parse errors */ }

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events")
            .Content(requestJson, "application/json")
            .StatusCodeShouldBeAccepted()
        );

        var processEventsJob = GetService<EventPostsJob>();
        await processEventsJob.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        var stats = await _eventQueue.GetQueueStatsAsync();
        // Save queue stats for audit - events may be silently rejected due to validation
        await SaveAuditFileAsync(testName, "queue-stats.json",
            JsonSerializer.Serialize(new { stats.Enqueued, stats.Completed, stats.Errors, stats.Deadletter, stats.Abandoned }));

        // Get events
        var events = await _eventRepository.GetAllAsync();
        PersistentEvent? ev = null;
        if (referenceId is not null)
        {
            ev = events.Documents.FirstOrDefault(e => String.Equals(e.ReferenceId, referenceId, StringComparison.OrdinalIgnoreCase));
            // If not found by ReferenceId, the camelCase/PascalCase key may have gone into Data bag
            ev ??= events.Documents.FirstOrDefault();
        }
        else
            ev = events.Documents.FirstOrDefault();

        if (ev is null)
        {
            // Event was rejected during processing - save diagnostic info
            await SaveAuditFileAsync(testName, "response.json",
                JsonSerializer.Serialize(new { error = "Event not found after processing", queue_stats = new { stats.Enqueued, stats.Completed, stats.Errors, stats.Deadletter }, total_events_in_repo = events.Documents.Count }));
            await SaveAuditFileAsync(testName, "elastic.json",
                JsonSerializer.Serialize(new { error = "No event document to retrieve" }));
            return;
        }

        // Get API response
        var response = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .AppendPath(ev.Id)
            .StatusCodeShouldBeOk()
        );

        string responseJson = await response.Content.ReadAsStringAsync(TestCancellationToken);
        await SaveAuditFileAsync(testName, "response.json", responseJson);

        // Get Elasticsearch document
        string esDoc = await GetElasticsearchDocumentAsync(GetEventsIndexPattern(), ev.Id);
        await SaveAuditFileAsync(testName, "elastic.json", esDoc);
    }
}
