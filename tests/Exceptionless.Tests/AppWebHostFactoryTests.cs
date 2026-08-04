using System.Net;
using System.Text;
using System.Text.Json;
using Foundatio.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Exceptionless.Tests;

public sealed class AppWebHostFactoryTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, """{"timed_out":false,"status":"yellow"}""", true)]
    [InlineData(HttpStatusCode.OK, """{"timed_out":false,"status":"green"}""", true)]
    [InlineData(HttpStatusCode.OK, """{"timed_out":true,"status":"yellow"}""", false)]
    [InlineData(HttpStatusCode.OK, """{"timed_out":false,"status":"red"}""", false)]
    [InlineData(HttpStatusCode.OK, """{"timed_out":false,"status":1}""", false)]
    [InlineData(HttpStatusCode.ServiceUnavailable, """{"timed_out":false,"status":"yellow"}""", false)]
    public void IsElasticsearchReady_ClusterHealthResponse_ReturnsExpectedResult(HttpStatusCode statusCode, string json, bool expected)
    {
        using var document = JsonDocument.Parse(json);

        bool isReady = AppWebHostFactory.IsElasticsearchReady(statusCode, document.RootElement);

        Assert.Equal(expected, isReady);
    }

    [Fact]
    public async Task ConfigureWebHost_MultipleFactories_IsolatesFileStorageByAppScope()
    {
        // Arrange
        await using var firstFactory = new AppWebHostFactory();
        await firstFactory.InitializeAsync();
        var firstStorage = firstFactory.Services.GetRequiredService<IFileStorage>();

        const string path = "scope-isolation/payload.txt";
        await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("payload")))
            await firstStorage.SaveFileAsync(path, stream, TestContext.Current.CancellationToken);

        await using var secondFactory = new AppWebHostFactory();
        await secondFactory.InitializeAsync();
        var secondStorage = secondFactory.Services.GetRequiredService<IFileStorage>();

        // Act
        await secondStorage.DeleteFilesAsync(await secondStorage.GetFileListAsync(cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.True(await firstStorage.ExistsAsync(path));
    }
}
