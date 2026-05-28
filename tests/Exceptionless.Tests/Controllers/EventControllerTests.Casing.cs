using System.Text.Json;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Controllers;

/// <summary>
/// Integration tests that verify events submitted with different JSON casing conventions
/// are processed correctly through the full pipeline (API → Queue → Job → ES).
/// Reproduces critical issues found in the serialization audit diff.
/// </summary>
public partial class EventControllerTests
{
    private static readonly DateTimeOffset TestUtcNow = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private DateTimeOffset GetRecentDate()
    {
        TimeProvider.SetUtcNow(TestUtcNow);
        return TestUtcNow.AddHours(-1);
    }

    private static string FormatDate(DateTimeOffset date) => date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss+00:00");

    [Fact]
    public async Task PostEvent_PascalCase_PreservesAllProperties()
    {
        // Arrange
        var recentDate = GetRecentDate();
        string recentDateStr = FormatDate(recentDate);
        string requestJson = $$"""
        {
            "Type": "error",
            "Message": "PascalCase error test",
            "Tags": ["integration", "pascal"],
            "ReferenceId": "pascal-int-001",
            "Count": 3,
            "Value": 99.9,
            "Geo": "51.5074,-0.1278",
            "Date": "{{recentDateStr}}",
            "@simple_error": {
                "Message": "Something broke",
                "Type": "System.Exception",
                "StackTrace": "   at Test.Run()"
            }
        }
        """;

        // Act
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

        // Assert
        var events = await _eventRepository.GetAllAsync();
        var ev = events.Documents.FirstOrDefault(e =>
            String.Equals(e.ReferenceId, "pascal-int-001", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(ev);
        Assert.Equal("error", ev.Type);
        Assert.Equal("PascalCase error test", ev.Message);
        Assert.Equal(3, ev.Count);
        Assert.Equal(99.9m, ev.Value);
        Assert.Equal(recentDate.Date, ev.Date.Date);
        Assert.Contains("integration", ev.Tags!);
        Assert.Contains("pascal", ev.Tags!);
    }

    [Fact]
    public async Task PostEvent_CamelCase_PreservesAllProperties()
    {
        // Arrange
        var recentDate = GetRecentDate();
        string recentDateStr = FormatDate(recentDate);
        string requestJson = $$"""
        {
            "type": "error",
            "message": "camelCase error test",
            "tags": ["integration", "camel"],
            "referenceId": "camel-int-001",
            "count": 2,
            "value": 55.5,
            "geo": "48.8566,2.3522",
            "date": "{{recentDateStr}}",
            "@simple_error": {
                "message": "Something broke",
                "type": "System.Exception",
                "stackTrace": "   at Test.Run()"
            }
        }
        """;

        // Act
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

        // Assert
        var events = await _eventRepository.GetAllAsync();
        var ev = events.Documents.FirstOrDefault(e =>
            String.Equals(e.ReferenceId, "camel-int-001", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(ev);
        Assert.Equal("error", ev.Type);
        Assert.Equal("camelCase error test", ev.Message);
        Assert.Equal(2, ev.Count);
        Assert.Equal(55.5m, ev.Value);
        Assert.Equal(recentDate.Date, ev.Date.Date);
        Assert.Contains("integration", ev.Tags!);
        Assert.Contains("camel", ev.Tags!);
    }

    [Fact]
    public async Task PostEvent_MixedCase_PreservesAllProperties()
    {
        // Arrange
        var recentDate = GetRecentDate();
        string recentDateStr = FormatDate(recentDate);
        string requestJson = $$"""
        {
            "TYPE": "error",
            "message": "Mixed CASE error test",
            "TAGS": ["integration", "mixed"],
            "reference_id": "mixed-int-001",
            "Count": 1,
            "value": 10.0,
            "GEO": "35.6762,139.6503",
            "Date": "{{recentDateStr}}",
            "@simple_error": {
                "message": "Mixed case exception",
                "type": "System.InvalidOperationException"
            }
        }
        """;

        // Act
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

        // Assert
        var events = await _eventRepository.GetAllAsync();
        var ev = events.Documents.FirstOrDefault(e =>
            String.Equals(e.ReferenceId, "mixed-int-001", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(ev);
        Assert.Equal("error", ev.Type);
        Assert.Equal("Mixed CASE error test", ev.Message);
        Assert.Equal(1, ev.Count);
        Assert.Equal(10.0m, ev.Value);
        Assert.Equal(recentDate.Date, ev.Date.Date);
        Assert.Contains("integration", ev.Tags!);
        Assert.Contains("mixed", ev.Tags!);
    }

    [Fact]
    public async Task PostEvent_DateOnlyInData_PreservedAsString()
    {
        // Arrange
        var recentDate = GetRecentDate();
        string recentDateStr = FormatDate(recentDate);
        string fullDateStr = recentDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        string requestJson = $$"""
        {
            "type": "log",
            "message": "Date-only test",
            "reference_id": "date-only-001",
            "date": "{{recentDateStr}}",
            "data": {
                "date_only": "2026-01-15",
                "full_date": "{{fullDateStr}}"
            }
        }
        """;

        // Act
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

        // Assert
        var events = await _eventRepository.GetAllAsync();
        var ev = events.Documents.FirstOrDefault(e =>
            String.Equals(e.ReferenceId, "date-only-001", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(ev);
        Assert.Equal(recentDate.Date, ev.Date.Date);
        Assert.NotNull(ev.Data);
        Assert.True(ev.Data.TryGetValue("date_only", out var dateOnly));

        Assert.IsType<string>(dateOnly);
        Assert.Equal("2026-01-15", dateOnly);
    }

    [Fact]
    public async Task PostEvent_PascalCase_ApiResponseHasCorrectFormat()
    {
        // Arrange
        var recentDate = GetRecentDate();
        string recentDateStr = FormatDate(recentDate);
        string requestJson = $$"""
        {
            "Type": "log",
            "Message": "Response format test",
            "ReferenceId": "format-001",
            "Date": "{{recentDateStr}}",
            "Value": 0,
            "@user": {
                "Identity": "pascal-user@example.com",
                "Name": "Pascal User",
                "Data": {
                    "PlanName": "premium"
                }
            }
        }
        """;

        // Act
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

        var response = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .AppendPath(ev.Id)
            .StatusCodeShouldBeOk()
        );

        string responseJson = await response.Content.ReadAsStringAsync(TestCancellationToken);
        using var doc = JsonDocument.Parse(responseJson);

        // Assert
        Assert.True(doc.RootElement.TryGetProperty("type", out var typeProp));
        Assert.Equal("log", typeProp.GetString());

        Assert.True(doc.RootElement.TryGetProperty("message", out var msgProp));
        Assert.Equal("Response format test", msgProp.GetString());

        Assert.True(doc.RootElement.TryGetProperty("reference_id", out var refProp));
        Assert.Equal("format-001", refProp.GetString());

        Assert.True(doc.RootElement.TryGetProperty("date", out var dateProp));
        Assert.NotNull(dateProp.GetString());
        Assert.True(DateTimeOffset.TryParse(dateProp.GetString(), out var parsedDate));
        Assert.Equal(recentDate.Date, parsedDate.Date);

        Assert.True(doc.RootElement.TryGetProperty("created_utc", out var createdUtcProp));
        Assert.True(createdUtcProp.GetString()?.EndsWith("Z", StringComparison.Ordinal) ?? false);

        Assert.True(doc.RootElement.TryGetProperty("data", out var dataProp));
        Assert.True(dataProp.TryGetProperty("@user", out var userProp));
        Assert.True(userProp.TryGetProperty("Identity", out var identityProp));
        Assert.Equal("pascal-user@example.com", identityProp.GetString());
        Assert.True(userProp.TryGetProperty("Name", out var nameProp));
        Assert.Equal("Pascal User", nameProp.GetString());
        Assert.True(userProp.TryGetProperty("Data", out var userDataProp));
        Assert.True(userDataProp.TryGetProperty("PlanName", out var planNameProp));
        Assert.Equal("premium", planNameProp.GetString());
        Assert.False(userProp.TryGetProperty("identity", out _));
        Assert.False(userProp.TryGetProperty("name", out _));
        Assert.False(userProp.TryGetProperty("data", out _));

        Assert.True(doc.RootElement.TryGetProperty("value", out var valueProp));
        Assert.Equal("0", valueProp.GetRawText());
    }
}
