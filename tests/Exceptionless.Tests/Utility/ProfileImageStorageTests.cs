using System.Text;
using Exceptionless.Web.Utility;
using Foundatio.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace Exceptionless.Tests.Utility;

public class ProfileImageStorageTests
{
    private const string UserId = "507f1f77bcf86cd799439011";

    [Fact]
    public async Task SaveAsync_WithPngImage_StoresImage()
    {
        // Arrange
        var storage = new InMemoryFileStorage(new InMemoryFileStorageOptions());
        var modelState = new ModelStateDictionary();
        var file = CreateFile([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        // Act
        var result = await ProfileImageStorage.SaveAsync(storage, file, "users", UserId, modelState, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.EndsWith(".png", result.FileName);
        Assert.True(await storage.ExistsAsync(result.Path));
        Assert.True(modelState.IsValid);
    }

    [Fact]
    public async Task SaveAsync_WithInvalidImage_AddsModelError()
    {
        // Arrange
        var storage = new InMemoryFileStorage(new InMemoryFileStorageOptions());
        var modelState = new ModelStateDictionary();
        var file = CreateFile(Encoding.UTF8.GetBytes("not an image"), "text/plain", "avatar.txt");

        // Act
        var result = await ProfileImageStorage.SaveAsync(storage, file, "users", UserId, modelState, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
        Assert.False(modelState.IsValid);
        Assert.Contains(modelState["file"]!.Errors, error => error.ErrorMessage.Contains("PNG", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveAsync_WithOversizedImage_AddsModelError()
    {
        // Arrange
        var storage = new InMemoryFileStorage(new InMemoryFileStorageOptions());
        var modelState = new ModelStateDictionary();
        var file = CreateFile(new byte[ProfileImageStorage.MaxFileSize + 1]);

        // Act
        var result = await ProfileImageStorage.SaveAsync(storage, file, "users", UserId, modelState, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
        Assert.False(modelState.IsValid);
        Assert.Contains(modelState["file"]!.Errors, error => error.ErrorMessage.Contains("1 MB", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeleteFromUrlAsync_WithStoredImageUrl_DeletesImage()
    {
        // Arrange
        var storage = new InMemoryFileStorage(new InMemoryFileStorageOptions());
        var modelState = new ModelStateDictionary();
        var file = CreateFile([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var result = await ProfileImageStorage.SaveAsync(storage, file, "users", UserId, modelState, TestContext.Current.CancellationToken);
        Assert.NotNull(result);

        // Act
        await ProfileImageStorage.DeleteFromUrlAsync(storage, $"/api/v2/users/{UserId}/avatar/{result.FileName}", "users", UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(await storage.ExistsAsync(result.Path));
    }

    private static FormFile CreateFile(byte[] bytes, string contentType = "image/png", string fileName = "avatar.png")
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
