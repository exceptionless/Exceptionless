using System.Text.Json;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Controllers;

/// <summary>
/// Integration tests that verify events submitted with different JSON casing conventions
/// are processed correctly through the full pipeline (API → Queue → Job → ES).
/// Reproduces critical issues found in the serialization audit diff.
/// </summary>
public sealed class EventCasingIntegrationTests : IntegrationTestsBase
{
    private readonly IEventRepository _eventRepository;
    private readonly IQueue<EventPost> _eventQueue;

    public EventCasingIntegrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _eventRepository = GetService<IEventRepository>();
        _eventQueue = GetService<IQueue<EventPost>>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await _eventQueue.DeleteQueueAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    // CRITICAL: PascalCase events should preserve type, message, tags, value
    // Audit found these become type=session with all data lost

    [Fact]
    public async Task PostEvent_PascalCase_PreservesAllProperties()
    {
        /* language=json */
        const string requestJson = """
        {
            "Type": "error",
            "Message": "PascalCase error test",
            "Tags": ["integration", "pascal"],
            "ReferenceId": "pascal-int-001",
            "Count": 3,
            "Value": 99.9,
            "Geo": "51.5074,-0.1278",
            "Date": "2026-05-20T12:00:00+00:00",
            "@simple_error": {
                "Message": "Something broke",
                "Type": "System.Exception",
                "StackTrace": "   at Test.Run()"
            }
        }
        """;

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events")
            .Content(requestJson, "application/json")
            .StatusCodeShouldBeAccepted()
        );

        var job = GetService<EventPostsJob>();
        await job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        var events = await _eventRepository.GetAllAsync();
        // Find our event by reference id
        var ev = events.Documents.FirstOrDefault(e =>
            String.Equals(e.ReferenceId, "pascal-int-001", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(ev);
        Assert.Equal("error", ev.Type);
        Assert.Equal("PascalCase error test", ev.Message);
        Assert.Equal(3, ev.Count);
        Assert.Equal(99.9m, ev.Value);
        Assert.Contains("integration", ev.Tags!);
        Assert.Contains("pascal", ev.Tags!);
    }

    [Fact]
    public async Task PostEvent_CamelCase_PreservesAllProperties()
    {
        /* language=json */
        const string requestJson = """
        {
            "type": "error",
            "message": "camelCase error test",
            "tags": ["integration", "camel"],
            "referenceId": "camel-int-001",
            "count": 2,
            "value": 55.5,
            "geo": "48.8566,2.3522",
            "date": "2026-05-20T12:00:00+00:00",
            "@simple_error": {
                "message": "Something broke",
                "type": "System.Exception",
                "stackTrace": "   at Test.Run()"
            }
        }
        """;

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events")
            .Content(requestJson, "application/json")
            .StatusCodeShouldBeAccepted()
        );

        var job = GetService<EventPostsJob>();
        await job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        var events = await _eventRepository.GetAllAsync();
        var ev = events.Documents.FirstOrDefault(e =>
            String.Equals(e.ReferenceId, "camel-int-001", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(ev);
        Assert.Equal("error", ev.Type);
        Assert.Equal("camelCase error test", ev.Message);
        Assert.Equal(2, ev.Count);
        Assert.Equal(55.5m, ev.Value);
        Assert.Contains("integration", ev.Tags!);
        Assert.Contains("camel", ev.Tags!);
    }

    [Fact]
    public async Task PostEvent_MixedCase_PreservesAllProperties()
    {
        /* language=json */
        const string requestJson = """
        {
            "TYPE": "error",
            "message": "Mixed CASE error test",
            "TAGS": ["integration", "mixed"],
            "reference_id": "mixed-int-001",
            "Count": 1,
            "value": 10.0,
            "GEO": "35.6762,139.6503",
            "Date": "2026-05-20T12:00:00+00:00",
            "@simple_error": {
                "message": "Mixed case exception",
                "type": "System.InvalidOperationException"
            }
        }
        """;

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events")
            .Content(requestJson, "application/json")
            .StatusCodeShouldBeAccepted()
        );

        var job = GetService<EventPostsJob>();
        await job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        var events = await _eventRepository.GetAllAsync();
        var ev = events.Documents.FirstOrDefault(e =>
            String.Equals(e.ReferenceId, "mixed-int-001", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(ev);
        Assert.Equal("error", ev.Type);
        Assert.Equal("Mixed CASE error test", ev.Message);
        Assert.Equal(1, ev.Count);
        Assert.Equal(10.0m, ev.Value);
        Assert.Contains("integration", ev.Tags!);
        Assert.Contains("mixed", ev.Tags!);
    }

    // ISSUE: Date-only strings should not be expanded to full DateTimeOffset

    [Fact]
    public async Task PostEvent_DateOnlyInData_PreservedAsString()
    {
        /* language=json */
        const string requestJson = """
        {
            "type": "log",
            "message": "Date-only test",
            "reference_id": "date-only-001",
            "date": "2026-05-20T12:00:00+00:00",
            "data": {
                "date_only": "2026-01-15",
                "full_date": "2026-05-20T12:00:00Z"
            }
        }
        """;

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events")
            .Content(requestJson, "application/json")
            .StatusCodeShouldBeAccepted()
        );

        var job = GetService<EventPostsJob>();
        await job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        var events = await _eventRepository.GetAllAsync();
        var ev = events.Documents.FirstOrDefault(e =>
            String.Equals(e.ReferenceId, "date-only-001", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(ev);
        Assert.NotNull(ev.Data);
        Assert.True(ev.Data.ContainsKey("date_only"));

        // Date-only strings should stay as strings (not be parsed to DateTimeOffset)
        var dateOnly = ev.Data["date_only"];
        Assert.IsType<string>(dateOnly);
        Assert.Equal("2026-01-15", dateOnly);
    }

    [Fact]
    public async Task PostEvent_PascalCase_ApiResponseHasCorrectFormat()
    {
        /* language=json */
        const string requestJson = """
        {
            "Type": "log",
            "Message": "Response format test",
            "ReferenceId": "format-001",
            "Date": "2026-05-20T12:00:00+00:00",
            "Value": 0
        }
        """;

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events")
            .Content(requestJson, "application/json")
            .StatusCodeShouldBeAccepted()
        );

        var job = GetService<EventPostsJob>();
        await job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        var events = await _eventRepository.GetAllAsync();
        var ev = events.Documents.FirstOrDefault(e =>
            String.Equals(e.ReferenceId, "format-001", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(ev);

        // Get via API and verify response format
        var response = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .AppendPath(ev.Id)
            .StatusCodeShouldBeOk()
        );

        string responseJson = await response.Content.ReadAsStringAsync(TestCancellationToken);
        var doc = JsonDocument.Parse(responseJson);

        // All properties should be snake_case in the response
        Assert.True(doc.RootElement.TryGetProperty("type", out var typeProp));
        Assert.Equal("log", typeProp.GetString());

        Assert.True(doc.RootElement.TryGetProperty("message", out var msgProp));
        Assert.Equal("Response format test", msgProp.GetString());

        Assert.True(doc.RootElement.TryGetProperty("reference_id", out var refProp));
        Assert.Equal("format-001", refProp.GetString());

        // value: 0 should serialize as integer 0, not 0.0
        Assert.True(doc.RootElement.TryGetProperty("value", out var valueProp));
        Assert.Equal("0", valueProp.GetRawText());
    }
}
