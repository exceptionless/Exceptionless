using System.Text;
using System.Text.Json;
using Exceptionless.Core.Services.SourceMaps;
using Xunit;

namespace Exceptionless.Tests.Services.SourceMaps;

public sealed class SourceMapDocumentTests
{
    [Fact]
    public void FindOriginalLocation_MappedSegment_ReturnsOriginalSourceAndName()
    {
        byte[] sourceMap = Encoding.UTF8.GetBytes("""{"version":3,"sources":["src/app.ts"],"names":["meaningfulFunction"],"mappings":"AAAAA"}""");
        var document = SourceMapDocument.Parse(sourceMap);

        var location = document.FindOriginalLocation(0, 12);

        Assert.NotNull(location);
        Assert.Equal("src/app.ts", location.Source);
        Assert.Equal(0, location.Line);
        Assert.Equal(0, location.Column);
        Assert.Equal("meaningfulFunction", location.Name);
    }

    [Fact]
    public void FindOriginalLocation_WithSourceRoot_CombinesRelativeSource()
    {
        byte[] sourceMap = Encoding.UTF8.GetBytes("""{"version":3,"sourceRoot":"https://cdn.example.com/source/","sources":["app.ts"],"names":[],"mappings":"AAAA"}""");
        var document = SourceMapDocument.Parse(sourceMap);

        var location = document.FindOriginalLocation(0, 0);

        Assert.NotNull(location);
        Assert.Equal("https://cdn.example.com/source/app.ts", location.Source);
    }

    [Fact]
    public void FindOriginalLocation_AfterUnmappedSegment_ReturnsNull()
    {
        byte[] sourceMap = Encoding.UTF8.GetBytes("""{"version":3,"sources":["src/app.ts"],"names":[],"mappings":"AAAA,U"}""");
        var document = SourceMapDocument.Parse(sourceMap);

        var location = document.FindOriginalLocation(0, 12);

        Assert.Null(location);
    }

    [Fact]
    public void Parse_IndexedSourceMap_ThrowsJsonException()
    {
        byte[] sourceMap = Encoding.UTF8.GetBytes("""{"version":3,"sections":[],"sources":[],"names":[],"mappings":""}""");

        var exception = Assert.Throws<JsonException>(() => SourceMapDocument.Parse(sourceMap));

        Assert.Contains("Indexed source maps", exception.Message);
    }

    [Fact]
    public void Parse_MissingMappings_ThrowsJsonException()
    {
        byte[] sourceMap = Encoding.UTF8.GetBytes("""{"version":3,"sources":[],"names":[]}""");

        var exception = Assert.Throws<JsonException>(() => SourceMapDocument.Parse(sourceMap));

        Assert.Contains("mappings", exception.Message);
    }

    [Fact]
    public void Parse_OversizedVersion_ThrowsJsonException()
    {
        byte[] sourceMap = Encoding.UTF8.GetBytes("""{"version":999999999999,"sources":[],"names":[],"mappings":""}""");

        var exception = Assert.Throws<JsonException>(() => SourceMapDocument.Parse(sourceMap));

        Assert.Contains("version 3", exception.Message);
    }

    [Fact]
    public void Parse_OversizedVlqValue_ThrowsJsonException()
    {
        byte[] sourceMap = Encoding.UTF8.GetBytes("""{"version":3,"sources":[],"names":[],"mappings":"gggggggA"}""");

        var exception = Assert.Throws<JsonException>(() => SourceMapDocument.Parse(sourceMap));

        Assert.Contains("too large", exception.Message);
    }

    [Fact]
    public void Parse_TooManyMappingSegments_ThrowsJsonException()
    {
        byte[] sourceMap = Encoding.UTF8.GetBytes("""{"version":3,"sources":["app.ts"],"names":[],"mappings":"AAAA,CAAC"}""");

        var exception = Assert.Throws<JsonException>(() => SourceMapDocument.Parse(sourceMap, maximumSegments: 1));

        Assert.Contains("too many mapping segments", exception.Message);
    }

    [Fact]
    public void Parse_TooManyGeneratedLines_ThrowsJsonException()
    {
        byte[] sourceMap = Encoding.UTF8.GetBytes("""{"version":3,"sources":[],"names":[],"mappings":";"}""");

        var exception = Assert.Throws<JsonException>(() => SourceMapDocument.Parse(sourceMap, maximumSegments: 1_000_000, maximumLines: 1));

        Assert.Contains("too many generated lines", exception.Message);
    }
}
