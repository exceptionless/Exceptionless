using System.Text.Json;
using System.Text.Json.Nodes;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Plugins;

public class SummaryDataTests : TestWithServices
{
    private readonly ITextSerializer _serializer;
    private readonly JsonSerializerOptions _jsonOptions;

    public SummaryDataTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
        _jsonOptions = GetService<JsonSerializerOptions>();
    }

    [Theory]
    [MemberData(nameof(Events))]
    public async Task EventSummaryData(string path)
    {
        string json = await File.ReadAllTextAsync(path, TestCancellationToken);
        Assert.NotNull(json);

        var ev = _serializer.Deserialize<PersistentEvent>(json);
        Assert.NotNull(ev);

        var data = GetService<FormattingPluginManager>().GetEventSummaryData(ev);
        var summary = new EventSummaryModel
        {
            Date = ev.Date,
            Id = ev.Id,
            TemplateKey = data.TemplateKey,
            Data = data.Data
        };

        string expectedContent = await File.ReadAllTextAsync(Path.ChangeExtension(path, "summary.json"), TestCancellationToken);
        var expected = JsonNode.Parse(expectedContent);
        var actual = JsonNode.Parse(JsonSerializer.Serialize(summary, _jsonOptions));
        Assert.True(JsonNode.DeepEquals(expected, actual),
            $"File: {Path.GetFileName(path)}\nExpected:\n{expected?.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}\n\nActual:\n{actual?.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}");
    }

    [Theory]
    [MemberData(nameof(Stacks))]
    public async Task StackSummaryData(string path)
    {
        string json = await File.ReadAllTextAsync(path, TestCancellationToken);
        Assert.NotNull(json);

        var stack = _serializer.Deserialize<Stack>(json);
        Assert.NotNull(stack);

        var data = GetService<FormattingPluginManager>().GetStackSummaryData(stack);
        var summary = new StackSummaryModel
        {
            Title = stack.Title,
            Status = stack.Status,
            Total = 1,
            Id = data.Id,
            TemplateKey = data.TemplateKey,
            Data = data.Data
        };

        string expectedContent = await File.ReadAllTextAsync(Path.ChangeExtension(path, "summary.json"), TestCancellationToken);
        var expected = JsonNode.Parse(expectedContent);
        var actual = JsonNode.Parse(JsonSerializer.Serialize(summary, _jsonOptions));
        Assert.True(JsonNode.DeepEquals(expected, actual),
            $"File: {Path.GetFileName(path)}\nExpected:\n{expected?.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}\n\nActual:\n{actual?.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}");
    }

    public static IEnumerable<object[]> Events
    {
        get
        {
            var result = new List<object[]>();
            foreach (string file in Directory.GetFiles(Path.Combine("..", "..", "..", "Search", "Data"), "event*.json", SearchOption.AllDirectories))
                if (!file.EndsWith("summary.json"))
                    result.Add([Path.GetFullPath(file)]);

            return result.ToArray();
        }
    }

    public static IEnumerable<object[]> Stacks
    {
        get
        {
            var result = new List<object[]>();
            foreach (string file in Directory.GetFiles(Path.Combine("..", "..", "..", "Search", "Data"), "stack*.json", SearchOption.AllDirectories))
                if (!file.EndsWith("summary.json"))
                    result.Add([Path.GetFullPath(file)]);

            return result.ToArray();
        }
    }
}
