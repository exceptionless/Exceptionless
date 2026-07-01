using System.Text;
using Foundatio.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Exceptionless.Tests;

[Collection("EventQueue")]
public sealed class AppWebHostFactoryTests
{
    [Fact]
    public async Task ConfigureWebHost_MultipleFactories_IsolatesFileStorageByAppScope()
    {
        await using var firstFactory = new AppWebHostFactory();
        await firstFactory.InitializeAsync();
        var firstStorage = firstFactory.Services.GetRequiredService<IFileStorage>();

        const string path = "scope-isolation/payload.txt";
        await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("payload")))
            await firstStorage.SaveFileAsync(path, stream, TestContext.Current.CancellationToken);

        await using var secondFactory = new AppWebHostFactory();
        await secondFactory.InitializeAsync();
        var secondStorage = secondFactory.Services.GetRequiredService<IFileStorage>();

        await secondStorage.DeleteFilesAsync(await secondStorage.GetFileListAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.True(await firstStorage.ExistsAsync(path));
    }
}
