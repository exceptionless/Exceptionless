using System.IO.Compression;
using Exceptionless.Core.Extensions;
using Xunit;

namespace Exceptionless.Tests.Extensions;

public class ByteArrayExtensionsTests : TestWithServices
{
    public ByteArrayExtensionsTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData("gzip")]
    [InlineData("deflate")]
    public void Decompress_WithinLimit_ReturnsDecompressedData(string encoding)
    {
        // Arrange
        byte[] data = new byte[10_000];
        Random.Shared.NextBytes(data);
        byte[] compressed = Compress(data, encoding);

        // Act
        byte[]? result = compressed.Decompress(encoding, data.Length);

        // Assert
        Assert.Equal(data, result);
    }

    [Theory]
    [InlineData("gzip")]
    [InlineData("deflate")]
    public void Decompress_OverLimit_ReturnsNull(string encoding)
    {
        // Arrange
        byte[] compressed = Compress(new byte[1024 * 1024], encoding);

        // Act
        byte[]? result = compressed.Decompress(encoding, 1024);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Decompress_UnsupportedEncoding_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new byte[1].Decompress("br", 1024));
    }

    private static byte[] Compress(byte[] data, string encoding)
    {
        using var output = new MemoryStream();
        using (Stream compression = encoding == "gzip"
            ? new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true)
            : new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            compression.Write(data);
        }

        return output.ToArray();
    }
}
