using System.Text;
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
}
