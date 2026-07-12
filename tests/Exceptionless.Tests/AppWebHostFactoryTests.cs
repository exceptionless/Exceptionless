using System.Text;
using System.Text.Json;
using Foundatio.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Exceptionless.Tests;

public sealed class AppWebHostFactoryTests
{
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

    [Fact]
    public async Task OpenApiEndpoint_DefaultRequest_ReturnsValidDocument()
    {
        // Arrange
        await using var factory = new AppWebHostFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        await factory.Server.WaitForReadyAsync();

        // Act
        using var response = await client.GetAsync("/docs/v2/openapi.json", TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("3.1.1", document.RootElement.GetProperty("openapi").GetString());
        Assert.NotEmpty(document.RootElement.GetProperty("paths").EnumerateObject());
    }
}
