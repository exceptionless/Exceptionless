using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Exceptionless.Tests.Api;

internal static class SnapshotTestHelper
{
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static async Task AssertMatchesJsonSnapshotAsync(string snapshotFileName, string actualJson, CancellationToken cancellationToken = default)
    {
        string snapshotPath = GetApiDataPath(snapshotFileName);
        string normalizedActualJson = NormalizeLineEndings(NormalizeJson(actualJson)).TrimEnd('\n');

        if (ShouldUpdateSnapshots())
            await File.WriteAllTextAsync(snapshotPath, normalizedActualJson, cancellationToken);

        string expectedJson = NormalizeLineEndings(await File.ReadAllTextAsync(snapshotPath, cancellationToken)).TrimEnd('\n');
        Assert.Equal(expectedJson, normalizedActualJson);
    }

    public static string Serialize(object value)
    {
        return NormalizeLineEndings(JsonSerializer.Serialize(value, s_jsonSerializerOptions));
    }

    public static string NormalizeJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, s_jsonSerializerOptions);
    }

    private static string GetApiDataPath(string fileName)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Api", "Data", fileName));
    }

    private static bool ShouldUpdateSnapshots()
    {
        return String.Equals(Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS"), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n");
    }
}
