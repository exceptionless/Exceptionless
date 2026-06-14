using System.Text;
using Exceptionless.Core.Utility;
using Exceptionless.Web.Utility;
using Foundatio.Storage;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace Exceptionless.Tests.Utility;

public class ProfileImageStorageTests : TestWithServices
{
    private const string UserId = "507f1f77bcf86cd799439011";

    public ProfileImageStorageTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task SaveAsync_WithPngImage_StoresImage()
    {
        // Arrange
        using var storage = GetService<IFileStorage>();
        var modelState = new ModelStateDictionary();
        using var file = CreateFile([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        // Act
        var result = await ProfileImageStorage.SaveAsync(storage, file.FormFile, "users", UserId, modelState, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.EndsWith(".png", result.FileName);
        Assert.True(await storage.ExistsAsync(result.Path));
        Assert.True(modelState.IsValid);
    }

    [Fact]
    public async Task SaveAsync_WithOrganizationImage_StoresImageUnderOrganizationPath()
    {
        // Arrange
        using var storage = GetService<IFileStorage>();
        var modelState = new ModelStateDictionary();
        using var file = CreateFile([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        // Act
        var result = await ProfileImageStorage.SaveAsync(storage, file.FormFile, "organizations", UserId, modelState, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith(OrganizationStoragePaths.GetProfileImagesPath(UserId), result.Path, StringComparison.Ordinal);
        Assert.True(await storage.ExistsAsync(result.Path));
    }

    [Fact]
    public async Task SaveAsync_WithInvalidImage_AddsModelError()
    {
        // Arrange
        using var storage = GetService<IFileStorage>();
        var modelState = new ModelStateDictionary();
        using var file = CreateFile(Encoding.UTF8.GetBytes("not an image"), "text/plain", "avatar.txt");

        // Act
        var result = await ProfileImageStorage.SaveAsync(storage, file.FormFile, "users", UserId, modelState, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
        Assert.False(modelState.IsValid);
        Assert.Contains(modelState["file"]!.Errors, error => error.ErrorMessage.Contains("PNG", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveAsync_WithOversizedImage_AddsModelError()
    {
        // Arrange
        using var storage = GetService<IFileStorage>();
        var modelState = new ModelStateDictionary();
        using var file = CreateFile(new byte[ProfileImageStorage.MaxFileSize + 1]);

        // Act
        var result = await ProfileImageStorage.SaveAsync(storage, file.FormFile, "users", UserId, modelState, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
        Assert.False(modelState.IsValid);
        Assert.Contains(modelState["file"]!.Errors, error => error.ErrorMessage.Contains(ProfileImageStorage.MaxFileSizeDisplayName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeleteAsync_WithStoredImageFileName_DeletesImage()
    {
        // Arrange
        using var storage = GetService<IFileStorage>();
        var modelState = new ModelStateDictionary();
        using var file = CreateFile([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var result = await ProfileImageStorage.SaveAsync(storage, file.FormFile, "users", UserId, modelState, TestContext.Current.CancellationToken);
        Assert.NotNull(result);

        // Act
        await ProfileImageStorage.DeleteAsync(storage, result.FileName, "users", UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(await storage.ExistsAsync(result.Path));
    }

    private static TestFormFile CreateFile(byte[] bytes, string contentType = "image/png", string fileName = "avatar.png")
    {
        var stream = new MemoryStream(bytes);
        var formFile = new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

        return new TestFormFile(formFile, stream);
    }

    private sealed record TestFormFile(FormFile FormFile, MemoryStream Stream) : IDisposable
    {
        public void Dispose()
        {
            Stream.Dispose();
        }
    }
}
