using Exceptionless.Core.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests TagSet serialization through ITextSerializer.
/// TagSet extends HashSet&lt;string?&gt; directly, so STJ handles it natively.
/// These tests guard against regressions.
/// </summary>
public class TagSetSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public TagSetSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_RoundTrip_PreservesValues()
    {
        // Arrange
        var tags = new TagSet();
        tags.Add("Error");
        tags.Add("Critical");
        tags.Add("Production");

        // Act
        string json = _serializer.SerializeToString(tags);
        var deserialized = _serializer.Deserialize<TagSet>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized.Count);
        Assert.Contains("Error", deserialized);
        Assert.Contains("Critical", deserialized);
        Assert.Contains("Production", deserialized);
    }

    [Fact]
    public void Deserialize_EmptyArray_ReturnsEmptyTagSet()
    {
        // Arrange
        /* language=json */
        const string json = """[]""";

        // Act
        var tags = _serializer.Deserialize<TagSet>(json);

        // Assert
        Assert.NotNull(tags);
        Assert.Empty(tags);
    }
}
