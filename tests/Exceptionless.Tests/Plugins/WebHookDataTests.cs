using System.Text.Json;
using System.Text.Json.Nodes;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Tests.Utility;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Plugins;

public sealed class WebHookDataTests : TestWithServices
{
    private readonly ITextSerializer _serializer;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly OrganizationData _organizationData;
    private readonly ProjectData _projectData;
    private readonly StackData _stackData;
    private readonly WebHookDataPluginManager _webHookData;
    private readonly FormattingPluginManager _formatter;

    public WebHookDataTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
        _jsonOptions = GetService<JsonSerializerOptions>();
        _organizationData = GetService<OrganizationData>();
        _projectData = GetService<ProjectData>();
        _stackData = GetService<StackData>();
        _webHookData = GetService<WebHookDataPluginManager>();
        _formatter = GetService<FormattingPluginManager>();
    }

    [Theory]
    [MemberData(nameof(WebHookData))]
    public async Task CreateFromEventAsync(string version, bool expectData)
    {
        object? data = await _webHookData.CreateFromEventAsync(GetWebHookDataContext(version));
        if (expectData)
        {
            string filePath = Path.GetFullPath(Path.Combine("..", "..", "..", "Plugins", "WebHookData", $"{version}.event.expected.json"));
            string expectedContent = await File.ReadAllTextAsync(filePath, TestCancellationToken);
            AssertJsonEquivalent(expectedContent, JsonSerializer.Serialize(data, _jsonOptions));
        }
        else
        {
            Assert.Null(data);
        }
    }

    [Theory]
    [MemberData(nameof(WebHookData))]
    public async Task CanCreateFromStackAsync(string version, bool expectData)
    {
        object? data = await _webHookData.CreateFromStackAsync(GetWebHookDataContext(version));
        if (expectData)
        {
            string filePath = Path.GetFullPath(Path.Combine("..", "..", "..", "Plugins", "WebHookData", $"{version}.stack.expected.json"));
            string expectedContent = await File.ReadAllTextAsync(filePath, TestCancellationToken);
            AssertJsonEquivalent(expectedContent, JsonSerializer.Serialize(data, _jsonOptions));
        }
        else
        {
            Assert.Null(data);
        }
    }

    public static IEnumerable<object[]> WebHookData => new List<object[]> {
            new object[] { "v0", false },
            new object[] { WebHook.KnownVersions.Version1, true },
            new object[] { WebHook.KnownVersions.Version2, true },
            new object[] { "v3", false }
        }.ToArray();

    private WebHookDataContext GetWebHookDataContext(string version)
    {
        string json = File.ReadAllText(Path.GetFullPath(Path.Combine("..", "..", "..", "ErrorData", "1477.expected.json")));

        var hook = new WebHook
        {
            Id = TestConstants.WebHookId,
            OrganizationId = TestConstants.OrganizationId,
            ProjectId = TestConstants.ProjectId,
            Url = "http://localhost:40000/test",
            EventTypes = [WebHook.KnownEventTypes.StackPromoted],
            Version = version,
            CreatedUtc = DateTime.UtcNow
        };

        var organization = _organizationData.GenerateSampleOrganization(GetService<BillingManager>(), GetService<BillingPlans>());
        var project = _projectData.GenerateSampleProject();

        var ev = _serializer.Deserialize<PersistentEvent>(json);
        Assert.NotNull(ev);
        ev.OrganizationId = TestConstants.OrganizationId;
        ev.ProjectId = TestConstants.ProjectId;
        ev.StackId = TestConstants.StackId;
        ev.Id = TestConstants.EventId;

        var stack = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: TestConstants.OrganizationId,
            projectId: TestConstants.ProjectId, title: _formatter.GetStackTitle(ev),
            signatureHash: "722e7afd4dca4a3c91f4d94fec89dfdc");

        stack.Tags.Clear();
        stack.Tags.Add("Test");
        stack.FirstOccurrence = stack.LastOccurrence = ev.Date.UtcDateTime;

        return new WebHookDataContext(hook, organization, project, stack, ev);
    }

    /// <summary>
    /// Compares two JSON strings semantically, ignoring null properties that may be
    /// present in expected but omitted by WhenWritingNull in actual.
    /// </summary>
    private static void AssertJsonEquivalent(string expectedJson, string actualJson)
    {
        var expected = JsonNode.Parse(expectedJson);
        var actual = JsonNode.Parse(actualJson);
        RemoveNullProperties(expected);
        RemoveNullProperties(actual);
        Assert.True(JsonNode.DeepEquals(expected, actual),
            $"Expected:\n{expected?.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}\n\nActual:\n{actual?.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}");
    }

    private static void RemoveNullProperties(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            return;
        }

        var keysToRemove = new List<string>();
        foreach (var prop in obj)
        {
            if (prop.Value is null)
            {
                keysToRemove.Add(prop.Key);
            }
            else if (prop.Value is JsonArray arr && arr.Count == 0)
            {
                keysToRemove.Add(prop.Key);
            }
            else
            {
                RemoveNullProperties(prop.Value);
            }
        }

        foreach (string key in keysToRemove)
        {
            obj.Remove(key);
        }
    }
}
