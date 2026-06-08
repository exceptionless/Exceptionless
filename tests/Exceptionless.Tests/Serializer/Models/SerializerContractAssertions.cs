using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

internal static class SerializerContractAssertions
{
    public static void IncludesProperties(string? json, params string[] propertyNames)
    {
        Assert.NotNull(json);

        foreach (string propertyName in propertyNames)
            Assert.Contains($"\"{propertyName}\":", json);
    }

    public static void ExcludesProperties(string? json, params string[] propertyNames)
    {
        Assert.NotNull(json);

        foreach (string propertyName in propertyNames)
            Assert.DoesNotContain($"\"{propertyName}\"", json);
    }
}
