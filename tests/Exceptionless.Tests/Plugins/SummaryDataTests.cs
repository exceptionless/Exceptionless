using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Newtonsoft.Json;
using Xunit;

namespace Exceptionless.Tests.Plugins;

public class SummaryDataTests : TestWithServices
{
    public SummaryDataTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [MemberData(nameof(Events))]
    public async Task EventSummaryData(string path)
    {
        var settings = GetService<JsonSerializerSettings>();
        settings.Formatting = Formatting.Indented;

        string json = await File.ReadAllTextAsync(path, TestCancellationToken);
        Assert.NotNull(json);

        var ev = json.FromJson<PersistentEvent>(settings);
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
        Assert.Equal(expectedContent, JsonConvert.SerializeObject(summary, settings));
    }

    [Theory]
    [MemberData(nameof(Stacks))]
    public async Task StackSummaryData(string path)
    {
        var settings = GetService<JsonSerializerSettings>();
        settings.Formatting = Formatting.Indented;

        string json = await File.ReadAllTextAsync(path, TestCancellationToken);
        Assert.NotNull(json);

        var stack = json.FromJson<Stack>(settings);
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
        Assert.Equal(expectedContent, JsonConvert.SerializeObject(summary, settings));
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
