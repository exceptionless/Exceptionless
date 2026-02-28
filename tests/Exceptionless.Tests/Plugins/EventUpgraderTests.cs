using System.Text.Json;
using System.Text.Json.Nodes;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Plugins.EventUpgrader;
using Xunit;

namespace Exceptionless.Tests.Plugins;

public sealed class EventUpgraderTests : TestWithServices
{
    private readonly EventUpgraderPluginManager _upgrader;
    private readonly EventParserPluginManager _parser;
    private readonly JsonSerializerOptions _jsonOptions;

    public EventUpgraderTests(ITestOutputHelper output) : base(output)
    {
        _upgrader = GetService<EventUpgraderPluginManager>();
        _parser = GetService<EventParserPluginManager>();
        _jsonOptions = GetService<JsonSerializerOptions>();
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public void ParseErrors(string errorFilePath)
    {
        string json = File.ReadAllText(errorFilePath);
        var ctx = new EventUpgraderContext(json);

        _upgrader.Upgrade(ctx);
        string expectedContent = File.ReadAllText(Path.ChangeExtension(errorFilePath, ".expected.json"));
        var expected = JsonNode.Parse(expectedContent);
        var actual = JsonNode.Parse(ctx.Documents.First().ToFormattedString(_jsonOptions));
        Assert.True(JsonNode.DeepEquals(expected, actual),
            $"File: {Path.GetFileName(errorFilePath)}\nExpected:\n{expected?.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}\n\nActual:\n{actual?.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}");

        var events = _parser.ParseEvents(ctx.Documents.ToFormattedString(_jsonOptions), 2, "exceptionless/2.0.0.0");
        Assert.Single(events);
    }

    public static IEnumerable<object[]> Errors
    {
        get
        {
            var result = new List<object[]>();
            foreach (string file in Directory.GetFiles(Path.Combine("..", "..", "..", "ErrorData"), "*.json", SearchOption.AllDirectories).Where(f => !f.EndsWith(".expected.json")))
                result.Add([Path.GetFullPath(file)]);

            return result.ToArray();
        }
    }
}
