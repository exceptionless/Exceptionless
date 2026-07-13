using System.IO.Compression;
using System.Text.Json;
using Exceptionless.Ingestion.Load;
using Xunit;

namespace Exceptionless.Tests.Benchmarks;

public sealed class IngestionLoadTests
{
    [Fact]
    public void ParseBothProtocolsAndBatchOptions()
    {
        LoadOptions options = LoadOptions.Parse([
            "--base-url", "https://localhost:7131/",
            "--project-id", "537650f3b77efe23a47914f4",
            "--submission-token", "submission",
            "--read-token", "read",
            "--protocol", "both",
            "--events", "1000",
            "--batch-size", "250",
            "--expected-persisted", "900"
        ]);

        Assert.Equal([IngestionProtocol.V2, IngestionProtocol.V3], options.Protocols);
        Assert.Equal(1000, options.EventCount);
        Assert.Equal(250, options.BatchSize);
        Assert.Equal(900, options.ExpectedPersisted);
    }

    [Fact]
    public void ParseV3DoesNotRequireReadCredential()
    {
        LoadOptions options = LoadOptions.Parse([
            "--base-url", "https://localhost:7131/",
            "--project-id", "537650f3b77efe23a47914f4",
            "--submission-token", "submission",
            "--protocol", "v3"
        ]);

        Assert.Equal([IngestionProtocol.V3], options.Protocols);
        Assert.Null(options.ReadToken);
    }

    [Fact]
    public async Task V2SingleEventWritesOneObjectAsync()
    {
        byte[] payload = await SerializeAsync(IngestionProtocol.V2, count: 1);
        using JsonDocument json = JsonDocument.Parse(payload);

        Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
        Assert.Equal("benchmark-reference-00000000", json.RootElement.GetProperty("reference_id").GetString());
        Assert.Equal("benchmark-reference", json.RootElement.GetProperty("tags")[0].GetString());
        Assert.Equal("Load.test.ActiveException0", json.RootElement.GetProperty("data").GetProperty("@simple_error").GetProperty("type").GetString());
    }

    [Fact]
    public async Task V2BatchWritesOneArrayAsync()
    {
        byte[] payload = await SerializeAsync(IngestionProtocol.V2, count: 100);
        using JsonDocument json = JsonDocument.Parse(payload);

        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.Equal(100, json.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task V3BatchWritesTopLevelValuesAsync()
    {
        byte[] payload = await SerializeAsync(IngestionProtocol.V3, count: 100);
        string[] lines = System.Text.Encoding.UTF8.GetString(payload).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(100, lines.Length);
        using JsonDocument first = JsonDocument.Parse(lines[0]);
        Assert.Equal("benchmark-reference-00000000", first.RootElement.GetProperty("reference_id").GetString());
        Assert.Equal("benchmark-reference", first.RootElement.GetProperty("tags")[0].GetString());
        Assert.Equal("Load.test.ActiveException0", first.RootElement.GetProperty("exception_type").GetString());
    }

    [Fact]
    public async Task GzipTracksRawAndTransferredBytesAsync()
    {
        LoadOptions options = CreateOptions() with { Compression = "gzip" };
        using var content = new StreamingEventContent(options, IngestionProtocol.V2, "benchmark-reference", DateTimeOffset.UtcNow, 0, 100);
        byte[] compressed = await content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        await using var source = new MemoryStream(compressed);
        await using var gzip = new GZipStream(source, CompressionMode.Decompress);
        await using var uncompressed = new MemoryStream();
        await gzip.CopyToAsync(uncompressed, TestContext.Current.CancellationToken);

        Assert.Equal(compressed.Length, content.TransferredBytes);
        Assert.Equal(uncompressed.Length, content.UncompressedBytes);
        Assert.True(content.TransferredBytes < content.UncompressedBytes);
    }

    private static async Task<byte[]> SerializeAsync(IngestionProtocol protocol, int count)
    {
        using var content = new StreamingEventContent(CreateOptions(), protocol, "benchmark-reference", DateTimeOffset.UtcNow, 0, count);
        return await content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
    }

    private static LoadOptions CreateOptions() => new(
        new Uri("https://localhost:7131/"),
        "537650f3b77efe23a47914f4",
        "submission",
        "read",
        null,
        null,
        [IngestionProtocol.V2, IngestionProtocol.V3],
        LoadEventType.Error,
        100,
        100,
        4,
        100,
        3,
        0,
        10,
        0,
        "none",
        "test",
        new string('x', 64),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMilliseconds(10));
}
